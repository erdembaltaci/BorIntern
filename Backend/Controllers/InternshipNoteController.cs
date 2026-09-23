using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> CreateNote(CreateNoteRequestDto request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _noteService.CreateNoteAsync(userId, request);
        return Created($"/api/notes/{result.Id}", result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyNotes()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _noteService.GetMyNotesAsync(userId);
        return Ok(result);
    }

    // Tekil not görüntüleme - sadece notun sahibi.
    [HttpGet("{noteId}")]
    public async Task<IActionResult> GetNoteById(int noteId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _noteService.GetNoteByIdAsync(userId, noteId);
        return Ok(result);
    }

    // Sahiplik kontrolü (sadece kendi notun mu) Service katmanında yapılıyor.
    [HttpPut("{noteId}")]
    public async Task<IActionResult> UpdateNote(int noteId, UpdateNoteRequestDto request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _noteService.UpdateNoteAsync(userId, noteId, request);
        return Ok(result);
    }

    // Fiziksel silme değil, soft delete (IsDeleted=true) - Service katmanında yapılıyor.
    [HttpDelete("{noteId}")]
    public async Task<IActionResult> DeleteNote(int noteId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _noteService.DeleteNoteAsync(userId, noteId);
        return NoContent();
    }

    // Silinen bir notu geri getirir.
    [HttpPost("{noteId}/restore")]
    public async Task<IActionResult> RestoreNote(int noteId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _noteService.RestoreNoteAsync(userId, noteId);
        return Ok(result);
    }
}
