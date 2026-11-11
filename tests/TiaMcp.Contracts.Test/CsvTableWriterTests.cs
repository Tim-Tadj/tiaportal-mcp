using System;
using TiaMcp.Contracts;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class CsvTableWriterTests
{
    [TestMethod]
    public void WriteUsesRfc4180RecordsWithoutFinalTerminator()
    {
        var rows = new[]
        {
            new SampleRow("Alpha", null, 1, true),
            new SampleRow("Beta", string.Empty, 2, false)
        };
        var columns = new[]
        {
            new TabularColumn<SampleRow>(
                "name",
                row => TabularCell.From(row.Name)),
            new TabularColumn<SampleRow>(
                "note",
                row => TabularCell.From(row.Note)),
            new TabularColumn<SampleRow>(
                "count",
                row => TabularCell.From((long)row.Count)),
            new TabularColumn<SampleRow>(
                "active",
                row => TabularCell.From(row.Active))
        };
        var presentation = TabularPresentation.Create("items", rows, columns);

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual(
            "name,note,count,active\r\n" +
            "\"Alpha\",,1,true\r\n" +
            "\"Beta\",\"\",2,false",
            result);
        Assert.IsFalse(result.EndsWith("\r\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WriteQuotesCommasQuotesAndEmbeddedNewlines()
    {
        var presentation = CreateStringTable(
            new[] { "a,b", "a\"b", "line1\r\nline2" });

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual(
            "value\r\n" +
            "\"a,b\"\r\n" +
            "\"a\"\"b\"\r\n" +
            "\"line1\r\nline2\"",
            result);
    }

    [TestMethod]
    public void WriteDistinguishesNullFromEmptyString()
    {
        var presentation = CreateStringTable(
            new string?[] { null, string.Empty });

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual("value\r\n\r\n\"\"", result);
    }

    [TestMethod]
    public void WriteReturnsHeaderForEmptyTable()
    {
        var presentation = CreateStringTable(Array.Empty<string?>());

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual("value", result);
    }

    [TestMethod]
    public void WriteQuotesUnsafeHeader()
    {
        var columns = new[]
        {
            new TabularColumn<string>(
                "display,name",
                value => TabularCell.From(value))
        };
        var presentation = TabularPresentation.Create(
            "items",
            new[] { "PLC" },
            columns);

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual("\"display,name\"\r\n\"PLC\"", result);
    }

    [TestMethod]
    public void WriteNormalisesDateTimeOffsetAsIso8601String()
    {
        var date = new DateTimeOffset(
            2026,
            7,
            29,
            8,
            10,
            0,
            TimeSpan.FromHours(10));
        var columns = new[]
        {
            new TabularColumn<DateTimeOffset>(
                "modifiedDate",
                value => TabularCell.From(value))
        };
        var presentation = TabularPresentation.Create(
            "items",
            new[] { date },
            columns);

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual(
            "modifiedDate\r\n\"2026-07-29T08:10:00.0000000+10:00\"",
            result);
    }

    [TestMethod]
    public void WriteDistinguishesStringsFromBooleanAndIntegerScalars()
    {
        var columns = new[]
        {
            new TabularColumn<int>(
                "stringBoolean",
                _ => TabularCell.From("true")),
            new TabularColumn<int>(
                "boolean",
                _ => TabularCell.From(true)),
            new TabularColumn<int>(
                "stringInteger",
                _ => TabularCell.From("42")),
            new TabularColumn<int>(
                "integer",
                _ => TabularCell.From(42L))
        };
        var presentation = TabularPresentation.Create(
            "items",
            new[] { 1 },
            columns);

        var result = CsvTableWriter.Write(presentation);

        Assert.AreEqual(
            "stringBoolean,boolean,stringInteger,integer\r\n" +
            "\"true\",true,\"42\",42",
            result);
    }

    [TestMethod]
    public void WriteRejectsUnpairedSurrogate()
    {
        var presentation = CreateStringTable(new[] { "\udfff" });

        Assert.ThrowsException<InvalidOperationException>(
            () => CsvTableWriter.Write(presentation));
    }

    [TestMethod]
    public void WriteRejectsControlCharactersOutsideRfc4180()
    {
        var presentation = CreateStringTable(new[] { "PLC\t1" });

        Assert.ThrowsException<InvalidOperationException>(
            () => CsvTableWriter.Write(presentation));
    }

    private static TabularPresentation CreateStringTable(string?[] values)
    {
        var rows = Array.ConvertAll(
            values,
            value => new StringRow(value));
        var columns = new[]
        {
            new TabularColumn<StringRow>(
                "value",
                row => TabularCell.From(row.Value))
        };

        return TabularPresentation.Create("items", rows, columns);
    }

    private sealed class StringRow
    {
        public StringRow(string? value)
        {
            Value = value;
        }

        public string? Value { get; }
    }

    private sealed class SampleRow
    {
        public SampleRow(
            string name,
            string? note,
            int count,
            bool active)
        {
            Name = name;
            Note = note;
            Count = count;
            Active = active;
        }

        public string Name { get; }

        public string? Note { get; }

        public int Count { get; }

        public bool Active { get; }
    }
}
