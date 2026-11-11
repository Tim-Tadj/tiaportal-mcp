using System;
using System.Text;

namespace TiaMcp.Contracts;

public static class CsvTableWriter
{
    public static string Write(TabularPresentation presentation)
    {
        if (presentation == null)
        {
            throw new ArgumentNullException(nameof(presentation));
        }

        var builder = new StringBuilder();

        for (var index = 0; index < presentation.Columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendString(builder, presentation.Columns[index]);
        }

        for (var rowIndex = 0; rowIndex < presentation.Rows.Count; rowIndex++)
        {
            builder.Append("\r\n");
            var row = presentation.Rows[rowIndex];

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (columnIndex > 0)
                {
                    builder.Append(',');
                }

                AppendCell(builder, row[columnIndex]);
            }
        }

        return builder.ToString();
    }

    private static void AppendCell(StringBuilder builder, TabularCell cell)
    {
        switch (cell.Kind)
        {
            case TabularCellKind.Null:
                return;

            case TabularCellKind.String:
                AppendDataString(builder, cell.GetString());
                return;

            case TabularCellKind.Boolean:
                builder.Append(cell.GetBoolean() ? "true" : "false");
                return;

            case TabularCellKind.Integer:
                builder.Append(cell.GetIntegerText());
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported tabular cell kind '{cell.Kind}'.");
        }
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        UnicodeScalarValidator.EnsureValid(value);
        EnsureRfc4180Text(value);

        if (value.Length == 0)
        {
            AppendQuoted(builder, value);
            return;
        }

        var requiresQuoting =
            value.IndexOf(',') >= 0 ||
            value.IndexOf('"') >= 0 ||
            value.IndexOf('\r') >= 0 ||
            value.IndexOf('\n') >= 0;

        if (!requiresQuoting)
        {
            builder.Append(value);
            return;
        }

        AppendQuoted(builder, value);
    }

    private static void AppendDataString(StringBuilder builder, string value)
    {
        UnicodeScalarValidator.EnsureValid(value);
        EnsureRfc4180Text(value);
        AppendQuoted(builder, value);
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(character);
            }
        }

        builder.Append('"');
    }

    private static void EnsureRfc4180Text(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if ((character < '\u0020' &&
                character != '\r' &&
                character != '\n') ||
                character == '\u007f')
            {
                throw new InvalidOperationException(
                    "CSV presentation text contains a control character that " +
                    "RFC 4180 does not permit.");
            }
        }
    }
}
