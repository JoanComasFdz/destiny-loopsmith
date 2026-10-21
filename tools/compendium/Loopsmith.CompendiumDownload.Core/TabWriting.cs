using System.Collections.Immutable;
using System.Text;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>A tab's grid → its <c>NN_&lt;Tab&gt;.md</c> and <c>.csv</c>, and a written CSV's row count back.</summary>
public static class TabWriting
{
    /// <summary>The tab's Markdown: its name as a heading, then its grid as a table (a cell's line breaks as <c>&lt;br&gt;</c>).</summary>
    public static string WriteMarkdown(string name, ImmutableArray<ImmutableArray<string>> rows) => $"## {name}\n\n{WriteMarkdownTable(rows)}";

    /// <summary>
    /// The tab's CSV (RFC 4180): a field quoted only when it holds a comma, a quote or a line break, quotes
    /// doubled, each row ended by <c>\r\n</c>.
    /// </summary>
    public static string WriteCsv(ImmutableArray<ImmutableArray<string>> rows)
    {
        var csv = new StringBuilder();
        foreach (var row in rows)
        {
            csv.Append(row is [""] ? "\"\"" : string.Join(',', row.Select(QuoteCsvField))).Append("\r\n");
        }

        return csv.ToString();
    }

    /// <summary>How many rows a CSV holds (a blank line is a row; a quoted line break isn't a new one).</summary>
    public static int CountCsvRows(string csv)
    {
        var rows = 0;
        var quoted = false;
        var rowOpen = false;
        for (var at = 0; at < csv.Length; at++)
        {
            var character = csv[at];
            if (character == '"')
            {
                quoted = !quoted;
                rowOpen = true;
            }
            else if (!quoted && character is '\r' or '\n')
            {
                rows++;
                rowOpen = false;
                if (character == '\r' && at + 1 < csv.Length && csv[at + 1] == '\n')
                {
                    at++;
                }
            }
            else
            {
                rowOpen = true;
            }
        }

        return rowOpen ? rows + 1 : rows;
    }

    private static string WriteMarkdownTable(ImmutableArray<ImmutableArray<string>> rows)
    {
        if (rows.IsEmpty)
        {
            return "_(empty)_\n";
        }

        var width = rows.Max(row => row.Length);
        var lines = rows.Select(row => "| " + string.Join(" | ", row.Concat(Enumerable.Repeat("", width - row.Length)).Select(EscapeMarkdownCell)) + " |").ToList();
        lines.Insert(1, "|" + string.Concat(Enumerable.Repeat("---|", width)));
        return string.Join('\n', lines) + "\n";
    }

    private static string EscapeMarkdownCell(string cell) => cell.Replace("|", "\\|").Replace("\n", "<br>");

    private static string QuoteCsvField(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
}
