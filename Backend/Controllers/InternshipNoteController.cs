using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

// Staj defteri. Kayıt sahipliği, durum kuralları (taslak/gönderildi/onaylı) ve mentor-stajyer ilişkisi
// Service katmanında denetlenir; burada sadece rol kapıları var.
[ApiController]
[Route("api/notes")]
[Authorize]
public class InternshipNoteController : ControllerBase
{
    private readonly IInternshipNoteService _noteService;

    public InternshipNoteController(IInternshipNoteService noteService)
    {
        _noteService = noteService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<IActionResult> CreateNote(CreateNoteRequestDto request)
    {
        var result = await _noteService.CreateNoteAsync(CurrentUserId, request);
        return Created($"/api/notes/{result.Id}", result);
    }

    // ?status=Draft|Submitted|Approved|ReturnedForRevision ve ?search= ile süzülür.
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyNotes(
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? status = null, string? search = null)
    {
        var result = await _noteService.GetMyNotesAsync(CurrentUserId, page, pageSize, status, search);
        return Ok(result);
    }

    // Mentorun inceleme kuyruğu: kendi gruplarındaki stajyerlerin gönderilmiş kayıtları (taslaklar görünmez).
    [Authorize(Roles = "Mentor")]
    [HttpGet("review")]
    public async Task<IActionResult> GetReviewQueue(
        int page = 1, int pageSize = Pagination.DefaultPageSize, string? status = null, string? search = null)
    {
        var result = await _noteService.GetReviewQueueAsync(CurrentUserId, page, pageSize, status, search);
        return Ok(result);
    }

    // Yazdırılabilir defter çıktısı için tarih aralığındaki kayıtlar (eskiden yeniye, en fazla 400).
    [HttpGet("export")]
    public async Task<IActionResult> Export(DateTime? from = null, DateTime? to = null)
    {
        var result = await _noteService.ExportNotesAsync(CurrentUserId, from, to);
        return Ok(result);
    }

    // Tekil görüntüleme - kaydın sahibi ya da (taslak değilse) sahibin mentoru.
    [HttpGet("{noteId}")]
    public async Task<IActionResult> GetNoteById(int noteId)
    {
        var result = await _noteService.GetNoteByIdAsync(CurrentUserId, noteId);
        return Ok(result);
    }

    // Sahiplik ve durum kontrolü (sadece taslak/düzeltme istenen kayıt) Service katmanında yapılıyor.
    [HttpPut("{noteId}")]
    public async Task<IActionResult> UpdateNote(int noteId, UpdateNoteRequestDto request)
    {
        var result = await _noteService.UpdateNoteAsync(CurrentUserId, noteId, request);
        return Ok(result);
    }

    // Fiziksel silme değil, soft delete (IsDeleted=true) - Service katmanında yapılıyor.
    [HttpDelete("{noteId}")]
    public async Task<IActionResult> DeleteNote(int noteId)
    {
        await _noteService.DeleteNoteAsync(CurrentUserId, noteId);
        return NoContent();
    }

    // Silinen bir kaydı geri getirir.
    [HttpPost("{noteId}/restore")]
    public async Task<IActionResult> RestoreNote(int noteId)
    {
        var result = await _noteService.RestoreNoteAsync(CurrentUserId, noteId);
        return Ok(result);
    }

    // Stajyer: kaydı mentora gönderir / gönderileni geri çeker.
    [HttpPost("{noteId}/submit")]
    public async Task<IActionResult> Submit(int noteId)
    {
        var result = await _noteService.SubmitNoteAsync(CurrentUserId, noteId);
        return Ok(result);
    }

    [HttpPost("{noteId}/withdraw")]
    public async Task<IActionResult> Withdraw(int noteId)
    {
        var result = await _noteService.WithdrawNoteAsync(CurrentUserId, noteId);
        return Ok(result);
    }

    // Mentor: gönderilmiş kaydı onaylar ya da düzeltme ister.
    [Authorize(Roles = "Mentor")]
    [HttpPost("{noteId}/review")]
    public async Task<IActionResult> Review(int noteId, ReviewNoteRequestDto request)
    {
        var result = await _noteService.ReviewNoteAsync(CurrentUserId, noteId, request);
        return Ok(result);
    }
}
