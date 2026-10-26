namespace FastDbExplorer.Wpf.Localization;

/// <summary>Texts for the password field and the encrypted SQL-password storage (separate file to keep this change self-contained).</summary>
public static class CredentialStrings
{
    public const string PasswordLabel = "رمز عبور";
    public const string ShowPassword = "نمایش رمز عبور";
    public const string HidePassword = "پنهان‌کردن رمز عبور";
    public const string ChangePassword = "تغییر رمز عبور";
    public const string CancelChangePassword = "انصراف از تغییر رمز";
    public const string StoredPasswordMaskLabel = "رمز ذخیره‌شده (پنهان)";

    public const string PasswordInputNote = "رمز را همین‌جا وارد کنید. با «ذخیره» یا «اتصال» رمزنگاری و فقط برای همین کاربر ویندوز (DPAPI) ذخیره می‌شود و بعد از آن هرگز نمایش داده نمی‌شود.";
    public const string StoredPasswordNote = "رمز این اتصال ذخیره شده و قابل مشاهده نیست. اگر رمز عوض شده، «تغییر رمز عبور» را بزنید و رمز جدید را ذخیره کنید.";
    public const string PasswordStorageNote = "رمزها هرگز در فایل پروفایل‌ها نوشته نمی‌شوند؛ رمز هر اتصال جداگانه، رمزنگاری‌شده و بدون امکان نمایش نگه‌داری می‌شود.";
    public const string RememberedTooltip = "رمز عبور این اتصال به‌صورت رمزنگاری‌شده ذخیره شده است";

    public const string PasswordRequired = "برای این اتصال رمز ذخیره‌شده‌ای وجود ندارد؛ رمز را در فرم وارد کنید و اتصال یا ذخیره را بزنید.";
    public const string StoredPasswordRejected = "رمز ذخیره‌شده پذیرفته نشد و پاک شد؛ رمز جدید را در فرم وارد کنید.";
    public const string PasswordClearedNote = "چون سرور یا نام کاربری تغییر کرد، رمز ذخیره‌شده پاک شد؛ رمز را دوباره وارد کنید.";
    public const string PasswordStoreFailed = "ذخیره امن رمز عبور ناموفق بود: ";
}
