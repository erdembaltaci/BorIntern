using Backend.Data;
using Backend.Dtos;
using Backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto> RegisterAsync(RegisterRequestDto request)
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

    public async Task<UserDto> LoginAsync(LoginRequestDto request)
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
}
