using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

// Sadece kimlik doğrulama (register/login/refresh/logout) burada. Profil görüntüleme/güncelleme
// UserController'a taşındı - tek sorumluluk ilkesi (SRP).
// Not: try/catch artık yok - Service'lerin fırlattığı hatalar ExceptionHandlingMiddleware
// tarafından merkezi olarak yakalanıp doğru HTTP koduna çevriliyor.
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // Kayıt: her zaman Intern + Pending olarak oluşturur, token vermez
    // (Pending kullanıcı, Register üzerinden token alıp korumalı endpoint'lere giremesin diye).
    [EnableRateLimiting("RegisterPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return Created($"/api/auth/{result.Id}", result);
    }

    // Giriş: sadece Active kullanıcılar başarılı olur, JWT + refresh token döner.
    // Rate limit: aynı IP'den 1 dakikada en fazla 5 deneme (brute-force koruması).
    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(result);
    }

    // Access token süresi dolunca, parola girmeden yeni bir access+refresh token çifti almak için.
    [EnableRateLimiting("RefreshPolicy")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return Ok(result);
    }

    // Refresh token'ı iptal eder - "çıkış yap" işlevi.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequestDto request)
    {
        await _authService.LogoutAsync(request);
        return NoContent();
    }
}
