using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IConfiguration _configuration;
    private readonly ILoginAttemptTracker _loginAttemptTracker;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IConfiguration configuration,
        ILoginAttemptTracker loginAttemptTracker)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _configuration = configuration;
        _loginAttemptTracker = loginAttemptTracker;
    }

    // Yeni kullanıcı her zaman Intern + Pending olarak oluşur - Role/Status kullanıcı isteğinden
    // ASLA alınmaz, burada sabitlenir. Token dönmüyoruz: onay gelmeden hiçbir korumalı endpoint'e giremesin.
    public async Task<UserDto> RegisterAsync(RegisterRequestDto request)
    {
        bool emailExists = await _userRepository.EmailExistsAsync(request.Email);
        if (emailExists)
        {
            throw new ConflictException("Bu email adresi zaten kayıtlı.");
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = UserRole.Intern,
            Status = UserStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt
        };
    }

    // Sırayla: kullanıcı var mı -> parola doğru mu -> Active mi. Üçü de geçerse JWT + refresh token üretilir.
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        // Hesap bazlı kilit: art arda hatalı denemeden sonra doğru şifre girilse bile bir süre giriş reddedilir.
        if (_loginAttemptTracker.IsLockedOut(request.Email))
        {
            throw new TooManyRequestsException("Çok fazla hatalı giriş denemesi. Lütfen daha sonra tekrar deneyin.");
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _loginAttemptTracker.RecordFailure(request.Email);
            throw new UnauthorizedException("Kullanıcı bulunamadı.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult != PasswordVerificationResult.Success)
        {
            _loginAttemptTracker.RecordFailure(request.Email);
            throw new UnauthorizedException("Geçersiz şifre.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new UnauthorizedException("Kullanıcı aktif değil.");
        }

        _loginAttemptTracker.Reset(request.Email);
        return await BuildAuthResponseAsync(user);
    }

    // Access token süresi dolunca, kullanıcı parolasını tekrar girmeden buraya refresh token'ını
    // gönderir; biz de geçerliyse yeni bir access+refresh token çifti üretiriz.
    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(TokenHasher.Hash(request.RefreshToken));
        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedException("Geçersiz veya süresi dolmuş refresh token.");
        }

        var user = await _userRepository.GetByIdAsync(storedToken.UserId);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new UnauthorizedException("Kullanıcı bulunamadı veya aktif değil.");
        }

        // Rotation: kullanılan refresh token'ı hemen iptal ediyoruz - her refresh token SADECE
        // bir kere kullanılabilir. Biri bu token'ı çalıp kullansa bile, gerçek kullanıcı bir sonraki
        // refresh denemesinde "geçersiz" hatası alır ve durumun farkına varır.
        storedToken.IsRevoked = true;
        await _refreshTokenRepository.SaveChangesAsync();

        return await BuildAuthResponseAsync(user);
    }

    // Logout: refresh token'ı iptal eder. Var olan access token, kendi süresi (1 saat) dolana
    // kadar teknik olarak hâlâ geçerlidir - bu, JWT sistemlerinde kabul edilen bir sınırlamadır.
    public async Task LogoutAsync(RefreshTokenRequestDto request)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(TokenHasher.Hash(request.RefreshToken));
        if (storedToken == null)
        {
            throw new NotFoundException("Refresh token bulunamadı.");
        }

        storedToken.IsRevoked = true;
        await _refreshTokenRepository.SaveChangesAsync();
    }

    // Login ve RefreshToken aynı çıktıyı üretiyor (yeni access token + yeni refresh token + user
    // bilgisi) - bu yüzden tek bir yardımcı metoda topladık.
    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user)
    {
        // İstemciye ham token verilir, veritabanına sadece özeti (hash) yazılır.
        string rawRefreshToken = GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Token = TokenHasher.Hash(rawRefreshToken),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user),
            RefreshToken = rawRefreshToken,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                Status = user.Status.ToString(),
                CreatedAt = user.CreatedAt
            }
        };
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Kriptografik olarak güvenli, rastgele bir metin - JWT değil, sadece tahmin edilemez bir "anahtar".
    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
