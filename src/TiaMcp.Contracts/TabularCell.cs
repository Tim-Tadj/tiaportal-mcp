using System;
using System.Globalization;

namespace TiaMcp.Contracts;

public enum TabularCellKind
{
    Null,
    String,
    Boolean,
    Integer
}

public readonly struct TabularCell : IEquatable<TabularCell>
{
    private readonly string? _text;
    private readonly bool _boolean;

    private TabularCell(TabularCellKind kind, string? text, bool boolean)
    {
        Kind = kind;
        _text = text;
        _boolean = boolean;
    }

    public TabularCellKind Kind { get; }

    public static TabularCell Null => default;

    public static TabularCell From(string? value)
    {
        return value == null
            ? Null
            : new TabularCell(TabularCellKind.String, value, false);
    }

    public static TabularCell From(bool value)
    {
        return new TabularCell(TabularCellKind.Boolean, null, value);
    }

    public static TabularCell From(bool? value)
    {
        return value.HasValue ? From(value.Value) : Null;
    }

    public static TabularCell From(long value)
    {
        return new TabularCell(
            TabularCellKind.Integer,
            value.ToString(CultureInfo.InvariantCulture),
            false);
    }

    public static TabularCell From(long? value)
    {
        return value.HasValue ? From(value.Value) : Null;
    }

    public static TabularCell From(ulong value)
    {
        return new TabularCell(
            TabularCellKind.Integer,
            value.ToString(CultureInfo.InvariantCulture),
            false);
    }

    public static TabularCell From(ulong? value)
    {
        return value.HasValue ? From(value.Value) : Null;
    }

    public static TabularCell From(DateTime value)
    {
        return From(value.ToString("O", CultureInfo.InvariantCulture));
    }

    public static TabularCell From(DateTime? value)
    {
        return value.HasValue ? From(value.Value) : Null;
    }

    public static TabularCell From(DateTimeOffset value)
    {
        return From(value.ToString("O", CultureInfo.InvariantCulture));
    }

    public static TabularCell From(DateTimeOffset? value)
    {
        return value.HasValue ? From(value.Value) : Null;
    }

    public string GetString()
    {
        if (Kind != TabularCellKind.String)
        {
            throw new InvalidOperationException("The tabular cell does not contain a string.");
        }

        return _text!;
    }

    public bool GetBoolean()
    {
        if (Kind != TabularCellKind.Boolean)
        {
            throw new InvalidOperationException("The tabular cell does not contain a Boolean.");
        }

        return _boolean;
    }

    public string GetIntegerText()
    {
        if (Kind != TabularCellKind.Integer)
        {
            throw new InvalidOperationException("The tabular cell does not contain an integer.");
        }

        return _text!;
    }

    public bool Equals(TabularCell other)
    {
        return Kind == other.Kind &&
            string.Equals(_text, other._text, StringComparison.Ordinal) &&
            _boolean == other._boolean;
    }

    public override bool Equals(object? obj)
    {
        return obj is TabularCell other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Kind;
            hashCode = (hashCode * 397) ^ (_text?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ _boolean.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(TabularCell left, TabularCell right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TabularCell left, TabularCell right)
    {
        return !left.Equals(right);
    }
}
