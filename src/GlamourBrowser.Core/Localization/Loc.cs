using System.Globalization;
using System.Reflection;
using System.Text;

namespace GlamourBrowser.Core.Localization;

/// <summary>
/// The plugin's texts in the client's language. The German text is the key (<c>Loc.T("Waffe ziehen")</c>); its
/// translations come from Strings.csv (columns de, en, fr, ja). A text without a translation, and every text before
/// <see cref="Load"/> (as in the tests), stays German.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> table = new();

    /// <summary>The loaded language's column ("de", "en", "fr" or "ja").</summary>
    public static string Language { get; private set; } = "de";

    /// <summary>The culture for numbers and dates in the loaded language.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Loads the texts of a language from the embedded Strings.csv; an unknown language falls back to English.</summary>
    public static void Load(string language) => Load(language, EmbeddedCsv());

    /// <summary>The embedded Strings.csv.</summary>
    public static string EmbeddedCsv()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GlamourBrowser.Core.Localization.Strings.csv")
                           ?? throw new InvalidOperationException("Strings.csv is not embedded");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Loads the texts of a language from CSV text with a header row naming the language columns.</summary>
    public static void Load(string language, string csv)
    {
        (Language, table) = Read(language, csv);
        Culture = CultureInfo.GetCultureInfo(Language switch { "en" => "en-US", "fr" => "fr-FR", "ja" => "ja-JP", _ => "de-DE" });
    }

    /// <summary>
    /// The texts of a language (German text → translation) and the language actually read: English when the CSV has
    /// no such column. Empty cells are left out, so their texts stay German.
    /// </summary>
    public static (string Language, Dictionary<string, string> Table) Read(string language, string csv)
    {
        var rows = Csv.Parse(csv);
        var loaded = new Dictionary<string, string>();
        if (rows.Count == 0)
            return ("de", loaded);
        var header = rows[0];
        if (!header.Contains(language))
            language = header.Contains("en") ? "en" : "de";
        var column = header.IndexOf(language);
        var key = header.IndexOf("de");
        foreach (var row in rows.Skip(1))
        {
            if (key >= 0 && key < row.Count && column < row.Count && row[key].Length > 0 && row[column].Length > 0)
                loaded[row[key]] = row[column];
        }
        return (language, loaded);
    }

    /// <summary>The text in the loaded language.</summary>
    public static string T(string german) => table.GetValueOrDefault(german, german);

    /// <summary>The text with its placeholders ({0}, {1} …) filled in, in the loaded language.</summary>
    public static string F(string german, params object?[] args) => string.Format(Culture, T(german), args);
}
