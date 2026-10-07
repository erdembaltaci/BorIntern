namespace Backend.Entities;

// Mentorun kendi grubuna gönderdiği duyuru. Grubun üyeleri (stajyerler) görür.
// Diğer kayıtlar gibi soft delete kullanır: silinen duyuru veritabanından kalkmaz, IsDeleted=true olur.
public class Announcement : SoftDeletableEntity
{
    public int GroupId { get; set; }

    // Duyuruyu yazan mentor (grubun sahibi).
    public int MentorId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
