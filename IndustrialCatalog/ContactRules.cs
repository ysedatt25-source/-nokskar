using System.Net.Mail;
using System.Text.RegularExpressions;

public static class ContactRules
{
    static readonly Regex AllowedPhoneChars = new(@"^\+?[\d\s().-]+$", RegexOptions.Compiled);

    public static bool TryEmail(string? value, out string normalized)
    {
        normalized = "";
        var raw = (value ?? "").Trim();
        if (raw.Length is < 3 or > 200 || raw.Any(char.IsWhiteSpace)) return false;
        if (!MailAddress.TryCreate(raw, out var parsed)) return false;
        normalized = parsed.Address.Trim();
        return normalized.Length > 2 && normalized.Contains('@') && normalized.Contains('.');
    }

    public static bool TryNormalizeTurkeyPhone(string? value, out string normalized)
    {
        normalized = "";
        var raw = (value ?? "").Trim();
        if (raw.Length is < 10 or > 40 || !AllowedPhoneChars.IsMatch(raw)) return false;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 14 && digits.StartsWith("0090", StringComparison.Ordinal)) digits = digits[4..];
        else if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal)) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal)) digits = digits[1..];

        if (digits.Length != 10) return false;
        normalized = digits;
        return true;
    }

    public static string TurkeyPhoneKey(string? value) =>
        TryNormalizeTurkeyPhone(value, out var normalized) ? normalized : "";
}
