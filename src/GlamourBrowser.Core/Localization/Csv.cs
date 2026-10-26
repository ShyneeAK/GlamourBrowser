using System.Text;

namespace GlamourBrowser.Core.Localization;

/// <summary>Comma-separated values as in RFC 4180: fields in double quotes may hold commas, line breaks and doubled quotes.</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                    field.Append(text[++i]);
                else
                    quoted = false;
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                EndRow();
            }
            else
                field.Append(c);
        }
        EndRow();
        return rows;

        void EndRow()
        {
            row.Add(field.ToString());
            field.Clear();
            // Blank lines carry no row.
            if (row.Count > 1 || row[0].Length > 0)
                rows.Add(row);
            row = new List<string>();
        }
    }

    /// <summary>One field for a CSV line: quoted when it holds a comma, a quote or a line break.</summary>
    public static string Field(string value)
        => value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
