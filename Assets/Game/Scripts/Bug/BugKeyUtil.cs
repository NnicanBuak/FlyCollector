// BugKeyUtil.cs
using System.Text.RegularExpressions;

public static class BugKeyUtil
{
    // вытащим из любой строки шаблон типа FLY14.002 / FLY21 / FLY9.004 и т.п.
    static readonly Regex keyRx = new Regex(@"FLY\d+(?:\.\d+)?", RegexOptions.IgnoreCase|RegexOptions.Compiled);

    public static string CanonicalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string key = TargetBugsRuntime.NormalizeKey(raw);
        if (string.IsNullOrEmpty(key)) return key;

        int dot = key.IndexOf('.');
        if (dot < 0) return key;

        string prefix = key.Substring(0, dot);
        string fraction = key.Substring(dot + 1);
        if (string.IsNullOrEmpty(fraction)) return key;

        string trimmedFraction = fraction.TrimEnd('0');
        return string.IsNullOrEmpty(trimmedFraction) ? prefix : $"{prefix}.{trimmedFraction}";
    }

    public static string ExtractKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Replace("(Clone)", "").Replace("_Variant", "").Trim();
        var m = keyRx.Match(raw);
        if (!m.Success) return null;
        return TargetBugsRuntime.NormalizeKey(m.Value);
    }
}
