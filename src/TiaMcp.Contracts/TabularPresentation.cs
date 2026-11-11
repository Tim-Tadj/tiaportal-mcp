using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TiaMcp.Contracts;

public sealed class TabularPresentation
{
    public const int MaximumRows = 200;
    public const int MaximumColumns = 32;

    private TabularPresentation(
        string name,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<TabularCell>> rows)
    {
        Name = name;
        Columns = columns;
        Rows = rows;
    }

    public string Name { get; }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<IReadOnlyList<TabularCell>> Rows { get; }

    public static TabularPresentation Create<T>(
        string name,
        IEnumerable<T> rows,
        IReadOnlyList<TabularColumn<T>> columns)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A presentation name is required.", nameof(name));
        }

        if (rows == null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        if (columns == null)
        {
            throw new ArgumentNullException(nameof(columns));
        }

        if (columns.Count == 0)
        {
            throw new ArgumentException(
                "At least one explicit column is required.",
                nameof(columns));
        }

        if (columns.Count > MaximumColumns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                $"A presentation cannot contain more than {MaximumColumns} columns.");
        }

        var columnCopy = CopyColumns(columns);
        var materialisedRows = MaterialiseRows(rows, columnCopy);

        return new TabularPresentation(
            name,
            new ReadOnlyCollection<string>(GetColumnNames(columnCopy)),
            new ReadOnlyCollection<IReadOnlyList<TabularCell>>(materialisedRows));
    }

    private static TabularColumn<T>[] CopyColumns<T>(
        IReadOnlyList<TabularColumn<T>> columns)
    {
        var copy = new TabularColumn<T>[columns.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index] ??
                throw new ArgumentException(
                    "Column definitions cannot contain null.",
                    nameof(columns));

            if (!names.Add(column.Name))
            {
                throw new ArgumentException(
                    $"The column name '{column.Name}' is duplicated.",
                    nameof(columns));
            }

            copy[index] = column;
        }

        return copy;
    }

    private static string[] GetColumnNames<T>(IReadOnlyList<TabularColumn<T>> columns)
    {
        var names = new string[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            names[index] = columns[index].Name;
        }

        return names;
    }

    private static List<IReadOnlyList<TabularCell>> MaterialiseRows<T>(
        IEnumerable<T> rows,
        IReadOnlyList<TabularColumn<T>> columns)
    {
        var result = new List<IReadOnlyList<TabularCell>>();

        foreach (var row in rows)
        {
            if (result.Count == MaximumRows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rows),
                    $"A presentation cannot contain more than {MaximumRows} rows.");
            }

            if (row is null)
            {
                throw new ArgumentException(
                    "Presentation rows cannot contain null.",
                    nameof(rows));
            }

            var cells = new TabularCell[columns.Count];
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                cells[columnIndex] = columns[columnIndex].ValueSelector(row);
            }

            result.Add(new ReadOnlyCollection<TabularCell>(cells));
        }

        return result;
    }
}
