using System.Text.RegularExpressions;

namespace GlamourBrowser.Core.Sources;

/// <summary>
/// German ENpcResident names carry grammar markers as plain text: [a] adjective ending, [p] weak noun ending,
/// [t] definite article. Resolved for a name standing on its own (nominative singular): without an article the
/// adjective takes the strong ending (-er/-e/-es), after [t] the weak one (-e); [p] is empty in the nominative.
/// </summary>
public static class GermanNames
{
    private static readonly Regex Marker = new(@"\[[a-z]\]", RegexOptions.Compiled);

    /// <param name="pronoun">ENpcResident.Pronoun: 0 masculine, 1 feminine, 2 neuter.</param>
    public static string Nominative(string singular, int pronoun)
    {
        var afterArticle = false;
        var name = Marker.Replace(singular, marker =>
        {
            switch (marker.Value)
            {
                case "[t]":
                    afterArticle = true;
                    return pronoun switch { 1 => "die", 2 => "das", _ => "der" };
                case "[a]":
                    return afterArticle ? "e" : pronoun switch { 1 => "e", 2 => "es", _ => "er" };
                default:
                    return "";
            }
        });
        // Names with an adjective start in lower case ("fahrend[a] Händlerin").
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }
}
