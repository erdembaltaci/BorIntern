import { AbstractControl, ValidationErrors } from '@angular/forms';

// Backend'in parola kuralıyla aynı (PasswordRules.cs): en az 8 karakter, bir küçük harf, bir büyük harf, bir rakam.
// Sunucu yine de denetler; buradaki kontrol sadece kullanıcıya anında geri bildirim vermek içindir.
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_PATTERN = /^(?=.*\p{Ll})(?=.*\p{Lu})(?=.*\d).+$/u;

/** Form grubu doğrulayıcısı: "newPassword" ile "confirmPassword" alanları aynı olmalı. */
export function passwordsMatch(newField: string, confirmField: string) {
  return (group: AbstractControl): ValidationErrors | null => {
    return group.get(newField)?.value === group.get(confirmField)?.value ? null : { mismatch: true };
  };
}
