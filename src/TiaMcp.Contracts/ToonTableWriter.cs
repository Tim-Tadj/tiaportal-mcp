using System;
using System.Globalization;
using System.Text;

namespace TiaMcp.Contracts;

public static class ToonTableWriter
{
    public const string SpecificationVersion = "4.1";

    public static string Write(TabularPresentation presentation)
    {
        if (presentation == null)
        {
            throw new ArgumentNullException(nameof(presentation));
        }

        var builder = new StringBuilder();
        AppendKey(builder, presentation.Name);

        if (presentation.Rows.Count == 0)
        {
            builder.Append(": []");
            return builder.ToString();
        }

        builder.Append('[');
        builder.Append(
            presentation.Rows.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append("]{");

        for (var index = 0; index < presentation.Columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            AppendKey(builder, presentation.Columns[index]);
        }

        builder.Append("}:");

        for (var rowIndex = 0; rowIndex < presentation.Rows.Count; rowIndex++)
        {
            builder.Append('\n');
            builder.Append("  ");

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
                builder.Append("null");
                break;

            case TabularCellKind.String:
                AppendString(builder, cell.GetString());
                break;

            case TabularCellKind.Boolean:
                builder.Append(cell.GetBoolean() ? "true" : "false");
                break;

            case TabularCellKind.Integer:
                builder.Append(cell.GetIntegerText());
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported tabular cell kind '{cell.Kind}'.");
        }
    }

    private static void AppendKey(StringBuilder builder, string value)
    {
        UnicodeScalarValidator.EnsureValid(value);

        if (IsUnquotedKey(value))
        {
            builder.Append(value);
            return;
        }

        AppendQuoted(builder, value);
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        UnicodeScalarValidator.EnsureValid(value);

        if (RequiresQuoting(value))
        {
            AppendQuoted(builder, value);
            return;
        }

        builder.Append(value);
    }

    private static bool IsUnquotedKey(string value)
    {
        if (value.Length == 0 || !IsAsciiKeyStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsAsciiKeyContinuation(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiKeyStart(char value)
    {
        return value == '_' ||
            (value >= 'A' && value <= 'Z') ||
            (value >= 'a' && value <= 'z');
    }

    private static bool IsAsciiKeyContinuation(char value)
    {
        return IsAsciiKeyStart(value) ||
            (value >= '0' && value <= '9') ||
            value == '.';
    }

    private static bool RequiresQuoting(string value)
    {
        if (value.Length == 0 ||
            value[0] == '-' ||
            value[0] == '#' ||
            value[0] == ' ' ||
            value[0] == '\t' ||
            value[value.Length - 1] == ' ' ||
            value[value.Length - 1] == '\t' ||
            string.Equals(value, "true", StringComparison.Ordinal) ||
            string.Equals(value, "false", StringComparison.Ordinal) ||
            string.Equals(value, "null", StringComparison.Ordinal) ||
            IsNumericLike(value))
        {
            return true;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character <= '\u001f' ||
                character == ':' ||
                character == '"' ||
                character == '\\' ||
                character == '[' ||
                character == ']' ||
                character == '{' ||
                character == '}' ||
                character == ',')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumericLike(string value)
    {
        var index = 0;

        if (value[index] == '+' || value[index] == '-')
        {
            index++;
            if (index == value.Length)
            {
                return false;
            }
        }

        var integerStart = index;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (index == integerStart)
        {
            return false;
        }

        if (index < value.Length && value[index] == '.')
        {
            index++;
            var fractionStart = index;
            while (index < value.Length && IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == fractionStart)
            {
                return false;
            }
        }

        if (index < value.Length &&
            (value[index] == 'e' || value[index] == 'E'))
        {
            index++;
            if (index < value.Length &&
                (value[index] == '+' || value[index] == '-'))
            {
                index++;
            }

            var exponentStart = index;
            while (index < value.Length && IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == exponentStart)
            {
                return false;
            }
        }

        return index == value.Length;
    }

    private static bool IsAsciiDigit(char value)
    {
        return value >= '0' && value <= '9';
    }

    private static void AppendQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;

                case '"':
                    builder.Append("\\\"");
                    break;

                case '\n':
                    builder.Append("\\n");
                    break;

                case '\r':
                    builder.Append("\\r");
                    break;

                case '\t':
                    builder.Append("\\t");
                    break;

                default:
                    if (character <= '\u001f')
                    {
                        builder.Append("\\u");
                        builder.Append(
                            ((int)character).ToString(
                                "x4",
                                CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
