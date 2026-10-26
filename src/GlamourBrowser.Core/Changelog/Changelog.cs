using System.Reflection;
using System.Text;

namespace GlamourBrowser.Core.Ui;

/// <param name="Lines">The entry's lines as written: "- " starts a point, other lines are plain text.</param>
public sealed record ChangelogEntry(Version Version, IReadOnlyList<string> Lines);

/// <summary>
/// What is new per version, from one Markdown file per language (Changelog/de.md …): "## 0.4.0.0" starts a version,
/// newest first. Shown after an update with the versions the player has not seen yet.
/// </summary>
public static class Changelog
{
    public static IReadOnlyList<ChangelogEntry> Parse(string markdown)
    {
        var entries = new List<ChangelogEntry>();
        Version? version = null;
        var lines = new List<string>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## ") && Version.TryParse(line[3..].Trim(), out var next))
            {
                Add();
                version = next;
                lines = [];
            }
            else if (version != null && line.Trim().Length > 0)
                lines.Add(line.Trim());
        }
        Add();
        return entries.OrderByDescending(e => e.Version).ToList();

        void Add()
        {
            if (version != null)
                entries.Add(new ChangelogEntry(version, lines));
        }
    }

    /// <summary>
    /// The entries after <paramref name="lastSeen"/> up to <paramref name="current"/>, newest first, so an update over
    /// several versions shows them all; without a last seen version (updated from a version before the changelog) every
    /// entry up to the current one.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Since(IReadOnlyList<ChangelogEntry> entries, Version? lastSeen, Version current)
        => entries.Where(e => e.Version <= current && (lastSeen == null || e.Version > lastSeen)).ToList();

    /// <summary>The embedded changelog of a language, English when there is none, else German.</summary>
    public static string Embedded(string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in new[] { language, "en", "de" })
        {
            using var stream = assembly.GetManifestResourceStream($"GlamourBrowser.Core.Changelog.{name}.md");
            if (stream == null)
                continue;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        return "";
    }
}
