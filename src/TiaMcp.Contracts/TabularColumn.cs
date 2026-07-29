using System;

namespace TiaMcp.Contracts;

public sealed class TabularColumn<T>
{
    public TabularColumn(string name, Func<T, TabularCell> valueSelector)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A column name is required.", nameof(name));
        }

        Name = name;
        ValueSelector = valueSelector ??
            throw new ArgumentNullException(nameof(valueSelector));
    }

    public string Name { get; }

    internal Func<T, TabularCell> ValueSelector { get; }
}
