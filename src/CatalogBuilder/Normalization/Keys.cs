using System.Globalization;
using System.Text;

namespace CatalogBuilder.Normalization;

public static class Keys
{
    /// <summary>Normalized vocabulary key: lower-case ASCII letters/digits separated by single dashes.</summary>
    public static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var pendingDash = false;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // é → e
            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingDash && sb.Length > 0) sb.Append('-');
                sb.Append(char.ToLowerInvariant(c));
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }
        return sb.ToString();
    }
}
