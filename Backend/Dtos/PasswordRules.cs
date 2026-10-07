namespace Backend.Dtos;

// Parola kuralları tek yerde: kayıt, parola değiştirme ve parola sıfırlama aynı kuralları kullanır.
// (Frontend'deki canlı kural listesi de bunları yansıtır; asıl denetim her zaman burada, sunucudadır.)
public static class PasswordRules
{
    public const int MinLength = 8;
    public const string MinLengthMessage = "Parola en az 8 karakter olmalı.";

    // En az bir küçük harf, bir büyük harf ve bir rakam (Türkçe harfler dahil: \p{Ll}, \p{Lu}).
    public const string Pattern = @"^(?=.*\p{Ll})(?=.*\p{Lu})(?=.*\d).+$";
    public const string PatternMessage = "Parola en az bir büyük harf, bir küçük harf ve bir rakam içermeli.";
}
