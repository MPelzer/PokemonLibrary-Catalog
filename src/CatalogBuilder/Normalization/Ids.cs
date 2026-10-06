using System.Text;

namespace CatalogBuilder.Normalization;

/// <summary>Project IDs as defined in catalog format v1, section 2.</summary>
public static class Ids
{
    /// <summary>TCGdex set id → set code: lower case, '.' becomes '-' (e.g. <c>sv03.5</c> → <c>sv03-5</c>).</summary>
    public static string SetCode(string sourceSetId)
    {
        var code = Keys.Slug(sourceSetId.Replace('.', '-'));
        return code.Length > 0 ? code : throw new ArgumentException($"Cannot derive set code from '{sourceSetId}'.");
    }

    public static string Set(string lang, string setCode) => $"{lang}/{setCode}";

    public static string Card(string setId, string printedNumber) => $"{setId}/{Number(printedNumber)}";

    /// <summary>Card group for international languages, which share TCGdex set ids and numbering.</summary>
    public static string InternationalGroup(string setCode, string printedNumber) => $"intl/{setCode}/{Number(printedNumber)}";

    public static string Print(string cardId, PrintKey key)
    {
        var sb = new StringBuilder(cardId).Append('/').Append(key.Finish);
        if (key.Edition is not null) sb.Append('.').Append(key.Edition);
        foreach (var tag in key.Tags) sb.Append('~').Append(tag);
        return sb.ToString();
    }

    /// <summary>Printed number; characters outside <c>[A-Za-z0-9._-]</c> are percent-encoded (source values are decoded first).</summary>
    public static string Number(string printedNumber)
    {
        var raw = Uri.UnescapeDataString(printedNumber.Trim());
        var sb = new StringBuilder(raw.Length);
        foreach (var b in Encoding.UTF8.GetBytes(raw))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
