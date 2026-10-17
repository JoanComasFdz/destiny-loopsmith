using System.Collections.Immutable;
using System.Text;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// Reads the snapshot's CSV text (RFC 4180, as Google's export writes it): quoted cells may hold commas, line breaks and
/// doubled quotes. Every row comes back as its cells; a row's number is its 1-based position, the spreadsheet's row.
/// </summary>
public static class CsvReading
{
    public static ImmutableArray<ImmutableArray<string>> ReadRows(string text)
    {
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<string>>();
        var row = ImmutableArray.CreateBuilder<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            switch (quoted, character)
            {
                case (true, '"') when next == '"':
                    cell.Append('"');
                    index++;
                    break;
                case (true, '"'):
                    quoted = false;
                    break;
                case (true, _):
                    cell.Append(character);
                    break;
                case (false, '"'):
                    quoted = true;
                    break;
                case (false, ','):
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case (false, '\r') when next == '\n':
                    break;
                case (false, '\n' or '\r'):
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row.ToImmutable());
                    row.Clear();
                    break;
                default:
                    cell.Append(character);
                    break;
            }

            index++;
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToImmutable());
        }

        return rows.ToImmutable();
    }
}
