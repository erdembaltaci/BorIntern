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
    private readonly IPasswordResetTokenRepository _resetTokenRepository;
    private readonly IEmailSender _emailSender;
    private readonly PasswordHasher<User> _passwordHasher = new();

    // "Şifremi unuttum" bağlantısı bu kadar dakika geçerlidir (e-postayla anında ulaşır).
    private const int ResetTokenMinutes = 30;

    // Yöneticinin ürettiği bağlantı elle iletildiği için daha uzun yaşar (yine tek kullanımlıktır, ham değer saklanmaz).
    private const int AdminResetTokenHours = 24;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IConfiguration configuration,
        ILoginAttemptTracker loginAttemptTracker,
        IPasswordResetTokenRepository resetTokenRepository,
        IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _configuration = configuration;
        _loginAttemptTracker = loginAttemptTracker;
        _resetTokenRepository = resetTokenRepository;
        _emailSender = emailSender;
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

    public async Task<AuthResponseDto> ChangePasswordAsync(int userId, ChangePasswordRequestDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new UnauthorizedException("Kullanıcı bulunamadı veya aktif değil.");
        }

        // Mevcut parola tahmin edilerek denenemesin: giriş ekranındaki hesap kilidi burada da geçerli.
        if (_loginAttemptTracker.IsLockedOut(user.Email))
        {
            throw new TooManyRequestsException("Çok fazla hatalı deneme. Lütfen daha sonra tekrar deneyin.");
        }

        // 401 DEĞİL 400 dönülür: istemci 401'i "oturum bitti" sanıp refresh/çıkış akışına girmesin.
        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            _loginAttemptTracker.RecordFailure(user.Email);
            throw new InvalidOperationException("Mevcut parola yanlış.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new InvalidOperationException("Yeni parola mevcut parolayla aynı olamaz.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _userRepository.SaveChangesAsync();
        _loginAttemptTracker.Reset(user.Email);

        // Parolayı değiştiren kişi başka bir cihazda oturum açık bırakmış ya da parolası ele geçirilmiş olabilir:
        // bütün refresh token'lar iptal edilir. (Eski access token'lar en fazla 1 saat daha geçerli kalır.)
        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);

        // Bu oturum çıkış yapmasın diye yeni bir token çifti verilir.
        return await BuildAuthResponseAsync(user);
    }

    public bool IsEmailEnabled => _emailSender.IsConfigured;

    public async Task<PasswordResetLinkDto> CreateResetLinkForUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        // Onay bekleyen/pasif hesapların zaten girişi yok; sıfırlama bağlantısı onlara anlamsız (ve kullanılamaz) olurdu.
        if (user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Sıfırlama bağlantısı yalnızca aktif kullanıcılar için üretilebilir.");
        }

        var (rawToken, expiresAt) = await IssueResetTokenAsync(user.Id, TimeSpan.FromHours(AdminResetTokenHours));
        return new PasswordResetLinkDto
        {
            UserId = user.Id,
            UserName = user.FullName,
            Link = BuildResetLink(rawToken),
            ExpiresAt = expiresAt
        };
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        // E-posta tanımlı değilken hiçbir şey yapılmaz: bağlantı üretilmez, loga da yazılmaz (canlıda loglarda geçerli
        // bir sıfırlama bağlantısı kalmasın). Bu durumda sıfırlamayı yönetici yapar (CreateResetLinkForUserAsync).
        if (!_emailSender.IsConfigured)
        {
            return;
        }

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim());

        // Kayıtsız ya da aktif olmayan (onay bekleyen/pasif) hesap: hiçbir şey yapılmaz, hata da verilmez.
        if (user == null || user.Status != UserStatus.Active)
        {
            return;
        }

        var (rawToken, _) = await IssueResetTokenAsync(user.Id, TimeSpan.FromMinutes(ResetTokenMinutes));
        string link = BuildResetLink(rawToken);

        await _emailSender.SendAsync(
            user.Email,
            "Pusula - Parola sıfırlama",
            $"Merhaba {user.FullName},\n\n" +
            $"Parolanı sıfırlamak için aşağıdaki bağlantıyı aç (bağlantı {ResetTokenMinutes} dakika geçerlidir ve yalnızca bir kez kullanılabilir):\n\n" +
            $"{link}\n\n" +
            "Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; parolan değişmez.");
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        const string invalidMessage = "Sıfırlama bağlantısı geçersiz veya süresi dolmuş. Lütfen yeniden iste.";

        var token = await _resetTokenRepository.GetByHashAsync(TokenHasher.Hash(request.Token.Trim()));
        if (token == null || token.UsedAt != null || token.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException(invalidMessage);
        }

        var user = await _userRepository.GetByIdAsync(token.UserId);
        if (user == null || user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(invalidMessage);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        token.UsedAt = DateTime.UtcNow;

        // Her iki depo da aynı (istek başına tek) DbContext'i paylaşır: parola değişikliği ve "bağlantı kullanıldı"
        // işareti tek SaveChanges ile, tek işlemde kaydedilir. Biri yazılıp diğeri yazılmamış bir durum oluşmaz.
        await _userRepository.SaveChangesAsync();

        // Hesap ele geçirildiği için sıfırlanıyorsa, saldırganın açık oturumları da kapanmalı; hesap kilidi de kalkar.
        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);
        _loginAttemptTracker.Reset(user.Email);
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

    // Bilerek rol/yetki YOK burada - JWT sadece kimliği taşır. Yetki, her istekte
    // CurrentUserTokenValidator tarafından veritabanından taze okunur; token'a gömülseydi,
    // rolü değişen/pasifleştirilen bir kullanıcı süresi dolana kadar eski yetkiyi taşırdı.
    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
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

    // Yeni bir sıfırlama anahtarı üretir: önceki kullanılmamış bağlantılar geçersiz kılınır (sadece EN SON üretilen çalışır),
    // veritabanına yalnızca özeti yazılır. Ham anahtar çağırana döner ve başka hiçbir yerde saklanmaz.
    private async Task<(string RawToken, DateTime ExpiresAt)> IssueResetTokenAsync(int userId, TimeSpan lifetime)
    {
        string rawToken = GenerateUrlSafeToken();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lifetime);

        await _resetTokenRepository.InvalidateActiveForUserAsync(userId, now);
        await _resetTokenRepository.AddAsync(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(rawToken),
            ExpiresAt = expiresAt
        });
        await _resetTokenRepository.SaveChangesAsync();

        return (rawToken, expiresAt);
    }

    private string BuildResetLink(string rawToken)
    {
        string frontendUrl = (_configuration["App:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
        return $"{frontendUrl}/sifre-sifirla?token={rawToken}";
    }

    // 32 bayt rastgele, URL'de kaçış gerektirmeyen (base64url) anahtar: e-postadaki bağlantıya olduğu gibi konabilir.
    private static string GenerateUrlSafeToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // Kriptografik olarak güvenli, rastgele bir metin - JWT değil, sadece tahmin edilemez bir "anahtar".
    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
