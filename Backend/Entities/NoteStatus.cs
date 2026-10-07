namespace Backend.Entities;

// Staj defteri kaydının yaşam döngüsü:
//   Draft ──(stajyer gönderir)──> Submitted ──(mentor)──> Approved
//     ^                                │
//     │                                └──(mentor düzeltme ister)──> ReturnedForRevision ──(stajyer düzenleyip tekrar gönderir)──> Submitted
//     └──(stajyer geri çeker)──────────┘
// Sadece Draft ve ReturnedForRevision kayıtlar düzenlenebilir/silinebilir; Approved resmi kayıttır, kilitlidir.
public enum NoteStatus
{
    Draft,
    Submitted,
    Approved,
    ReturnedForRevision
}
