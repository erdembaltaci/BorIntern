using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;

namespace Backend.Services;

public class InternshipNoteService : IInternshipNoteService
{
    // Dışa aktarmada tek seferde en fazla bu kadar kayıt döner (yazdırılabilir bir defter için fazlasıyla yeterli).
    private const int ExportLimit = 400;

    private readonly IInternshipNoteRepository _noteRepository;
    private readonly IGroupMemberRepository _groupMemberRepository;
    private readonly IUserRepository _userRepository;

    public InternshipNoteService(
        IInternshipNoteRepository noteRepository,
        IGroupMemberRepository groupMemberRepository,
        IUserRepository userRepository)
    {
        _noteRepository = noteRepository;
        _groupMemberRepository = groupMemberRepository;
        _userRepository = userRepository;
    }

    // Kayıt her zaman token'daki kullanıcıya (userId) ait olarak oluşturulur -
    // istekte "başkası adına" kayıt eklenmesine hiç izin verilmiyor.
    public async Task<InternshipNoteDto> CreateNoteAsync(int userId, CreateNoteRequestDto request)
    {
        var day = (request.NoteDate ?? DateTime.UtcNow).Date;
        EnsureNotInFuture(day);

        if (await _noteRepository.ExistsOnDayAsync(userId, day, excludeNoteId: null))
        {
            throw new ConflictException("Bu güne ait bir defter kaydın zaten var; onu düzenleyebilirsin.");
        }

        var note = new InternshipNote
        {
            UserId = userId,
            NoteDate = day,
            Title = (request.Title ?? string.Empty).Trim(),
            Content = (request.Content ?? string.Empty).Trim(),
            Learned = (request.Learned ?? string.Empty).Trim(),
            HoursSpent = request.HoursSpent,
            Tags = NormalizeTags(request.Tags)
        };

        await _noteRepository.AddAsync(note);
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task<PagedResultDto<InternshipNoteDto>> GetMyNotesAsync(int callerId, int page, int pageSize, string? status, string? search)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (notes, totalCount) = await _noteRepository.GetPagedByUserIdAsync(
            callerId, page, pageSize, ParseStatusFilter(status), Pagination.NormalizeSearch(search));
        return PagedResultDto<InternshipNoteDto>.Create(await MapAllAsync(notes), page, pageSize, totalCount);
    }

    public async Task<InternshipNoteDto> GetNoteByIdAsync(int callerId, int noteId)
    {
        var note = await _noteRepository.GetByIdAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Defter kaydı bulunamadı.");
        }

        if (note.UserId != callerId)
        {
            // Sahibi değilsek, sadece sahibin mentoru ve kayıt taslak değilse görebiliriz.
            bool isMentorOfAuthor = note.Status != NoteStatus.Draft
                && await _groupMemberRepository.IsUserInMentorGroupAsync(callerId, note.UserId);
            if (!isMentorOfAuthor)
            {
                throw new ForbiddenException("Bu kayıt size ait değil.");
            }
        }

        return await MapOneAsync(note);
    }

    public async Task<InternshipNoteDto> UpdateNoteAsync(int userId, int noteId, UpdateNoteRequestDto request)
    {
        var note = await GetOwnedNoteAsync(userId, noteId);
        EnsureEditable(note);

        // Tarih değiştiyse yeni güne ait başka kayıt olmamalı.
        if (request.NoteDate.HasValue && request.NoteDate.Value.Date != note.NoteDate?.Date)
        {
            var day = request.NoteDate.Value.Date;
            EnsureNotInFuture(day);
            if (await _noteRepository.ExistsOnDayAsync(userId, day, excludeNoteId: note.Id))
            {
                throw new ConflictException("Bu güne ait başka bir defter kaydın zaten var.");
            }

            note.NoteDate = day;
        }

        note.Title = (request.Title ?? string.Empty).Trim();
        note.Content = (request.Content ?? string.Empty).Trim();
        note.Learned = (request.Learned ?? string.Empty).Trim();
        note.HoursSpent = request.HoursSpent;
        note.Tags = NormalizeTags(request.Tags);
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task DeleteNoteAsync(int userId, int noteId)
    {
        var note = await GetOwnedNoteAsync(userId, noteId);
        EnsureEditable(note);

        // Fiziksel silme yok - proje kararımız gereği soft delete (IsDeleted) kullanıyoruz.
        // AppDbContext'teki query filter sayesinde bu kayıt artık listelerde görünmeyecek.
        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        await _noteRepository.SaveChangesAsync();
    }

    public async Task<InternshipNoteDto> RestoreNoteAsync(int userId, int noteId)
    {
        // Silinmiş kayıtları da görebilen özel metodu kullanıyoruz - normal GetByIdAsync,
        // global query filter yüzünden silinmiş bir kaydı zaten hiç döndürmez.
        var note = await _noteRepository.GetByIdIncludingDeletedAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Defter kaydı bulunamadı.");
        }

        if (note.UserId != userId)
        {
            throw new ForbiddenException("Bu kayıt size ait değil.");
        }

        if (!note.IsDeleted)
        {
            throw new InvalidOperationException("Bu kayıt zaten silinmemiş.");
        }

        // Silme sırasında aynı güne yeni kayıt açılmış olabilir; iki kayıt çakışmasın.
        if (note.NoteDate.HasValue && await _noteRepository.ExistsOnDayAsync(userId, note.NoteDate.Value, excludeNoteId: note.Id))
        {
            throw new ConflictException("O güne yeni bir kayıt eklendiği için bu kayıt geri getirilemiyor.");
        }

        note.IsDeleted = false;
        note.DeletedAt = null;
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task<InternshipNoteDto> SubmitNoteAsync(int userId, int noteId)
    {
        var note = await GetOwnedNoteAsync(userId, noteId);
        EnsureEditable(note);

        // Eski (başlıksız) notlar da taslak olarak kaldı; mentora gitmeden önce tamamlanmalı.
        if (string.IsNullOrWhiteSpace(note.Title) || string.IsNullOrWhiteSpace(note.Content))
        {
            throw new InvalidOperationException("Göndermeden önce kayda bir başlık ve yapılan işi ekle.");
        }

        note.Status = NoteStatus.Submitted;
        note.SubmittedAt = DateTime.UtcNow;
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task<InternshipNoteDto> WithdrawNoteAsync(int userId, int noteId)
    {
        var note = await GetOwnedNoteAsync(userId, noteId);

        if (note.Status != NoteStatus.Submitted)
        {
            throw new ConflictException("Sadece mentorun henüz değerlendirmediği kayıt geri çekilebilir.");
        }

        note.Status = NoteStatus.Draft;
        note.SubmittedAt = null;
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task<PagedResultDto<InternshipNoteDto>> GetReviewQueueAsync(int mentorId, int page, int pageSize, string? status, string? search)
    {
        var filter = ParseStatusFilter(status);
        if (filter == NoteStatus.Draft)
        {
            throw new InvalidOperationException("Taslaklar stajyerin özelidir, mentora gösterilmez.");
        }

        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var (notes, totalCount) = await _noteRepository.GetPagedForMentorAsync(mentorId, page, pageSize, filter, Pagination.NormalizeSearch(search));
        return PagedResultDto<InternshipNoteDto>.Create(await MapAllAsync(notes), page, pageSize, totalCount);
    }

    public async Task<InternshipNoteDto> ReviewNoteAsync(int mentorId, int noteId, ReviewNoteRequestDto request)
    {
        var note = await _noteRepository.GetByIdAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Defter kaydı bulunamadı.");
        }

        // Mentor sahiplik kontrolü: sadece kendi grubundaki bir stajyerin kaydını değerlendirebilir.
        if (!await _groupMemberRepository.IsUserInMentorGroupAsync(mentorId, note.UserId))
        {
            throw new ForbiddenException("Bu stajyer sizin grubunuzda değil.");
        }

        if (note.Status != NoteStatus.Submitted)
        {
            throw new ConflictException("Sadece gönderilmiş ve henüz değerlendirilmemiş kayıtlar değerlendirilebilir.");
        }

        var comment = (request.Comment ?? string.Empty).Trim();
        if (!request.Approve && comment.Length == 0)
        {
            throw new InvalidOperationException("Düzeltme isterken stajyerin ne yapması gerektiğini yaz.");
        }

        note.Status = request.Approve ? NoteStatus.Approved : NoteStatus.ReturnedForRevision;
        note.MentorComment = comment;
        note.ReviewedAt = DateTime.UtcNow;
        note.ReviewedByUserId = mentorId;
        await _noteRepository.SaveChangesAsync();

        return await MapOneAsync(note);
    }

    public async Task<List<InternshipNoteDto>> ExportNotesAsync(int userId, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        {
            throw new InvalidOperationException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        var notes = await _noteRepository.GetRangeByUserIdAsync(userId, from, to, ExportLimit);
        return await MapAllAsync(notes);
    }

    // ------------------------------------------------------------------ yardımcılar

    private async Task<InternshipNote> GetOwnedNoteAsync(int userId, int noteId)
    {
        var note = await _noteRepository.GetByIdAsync(noteId);
        if (note == null)
        {
            throw new NotFoundException("Defter kaydı bulunamadı.");
        }

        // Görev sahipliği kontrolüyle aynı mantık: sadece kendi kaydını değiştirebilirsin.
        if (note.UserId != userId)
        {
            throw new ForbiddenException("Bu kayıt size ait değil.");
        }

        return note;
    }

    // Gönderilmiş kayıt mentorun önünde, onaylı kayıt resmi kayıt: ikisi de değiştirilemez.
    private static void EnsureEditable(InternshipNote note)
    {
        if (note.Status is NoteStatus.Submitted or NoteStatus.Approved)
        {
            throw new ConflictException(note.Status == NoteStatus.Approved
                ? "Onaylanmış kayıt değiştirilemez."
                : "Mentora gönderilmiş kayıt değiştirilemez; önce geri çekmelisin.");
        }
    }

    // Saat dilimi farkı için 1 günlük pay bırakılır (Türkiye UTC+3: gece yarısından sonra "bugün" UTC'de hâlâ dün olabilir).
    private static void EnsureNotInFuture(DateTime day)
    {
        if (day > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new InvalidOperationException("Gelecek tarihli kayıt eklenemez.");
        }
    }

    // "ef core,  JWT ,, " -> "ef core, JWT": boşlukları ve boş parçaları temizler, tekrarları (büyük/küçük harf duyarsız) atar.
    private static string NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return string.Empty;
        }

        var parts = tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", parts);
    }

    // Boş değer "filtre yok"; geçersiz bir metin sessizce yok sayılmaz, istemciye hata olarak döner.
    private static NoteStatus? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<NoteStatus>(status.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException("Geçersiz kayıt durumu.");
        }

        return parsed;
    }

    // Yazar ve değerlendiren mentorun adları, kayıt başına ayrı sorgu atmamak için tek seferde (toplu) getirilir.
    private async Task<List<InternshipNoteDto>> MapAllAsync(List<InternshipNote> notes)
    {
        var ids = notes.Select(n => n.UserId).Concat(notes.Where(n => n.ReviewedByUserId.HasValue).Select(n => n.ReviewedByUserId!.Value));
        var names = (await _userRepository.GetByIdsAsync(ids)).ToDictionary(u => u.Id, u => u.FullName);
        return notes.Select(n => MapToDto(n, names)).ToList();
    }

    private async Task<InternshipNoteDto> MapOneAsync(InternshipNote note)
    {
        return (await MapAllAsync(new List<InternshipNote> { note }))[0];
    }

    private static InternshipNoteDto MapToDto(InternshipNote note, Dictionary<int, string> names)
    {
        return new InternshipNoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            Learned = note.Learned,
            HoursSpent = note.HoursSpent,
            Tags = note.Tags,
            NoteDate = note.NoteDate,
            Status = note.Status.ToString(),
            SubmittedAt = note.SubmittedAt,
            MentorComment = note.MentorComment,
            ReviewedAt = note.ReviewedAt,
            ReviewedByName = note.ReviewedByUserId.HasValue ? names.GetValueOrDefault(note.ReviewedByUserId.Value, string.Empty) : string.Empty,
            UserId = note.UserId,
            UserName = names.GetValueOrDefault(note.UserId, string.Empty),
            CreatedAt = note.CreatedAt
        };
    }
}
