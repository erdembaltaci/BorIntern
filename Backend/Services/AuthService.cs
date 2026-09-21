using Backend.Data;
using Backend.Dtos;
using Backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        bool emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            throw new InvalidOperationException("Bu email adresi zaten kayıtlı.");
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

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user),
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

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            throw new InvalidOperationException("Kullanıcı bulunamadı.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult != PasswordVerificationResult.Success)
        {
            throw new InvalidOperationException("Geçersiz şifre.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Kullanıcı aktif değil.");
        }

        return new AuthResponseDto
        {
            Token = GenerateJwtToken(user),
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

}
