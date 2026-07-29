using System;
using TiaMcp.Contracts;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class ToonTableWriterTests
{
    [TestMethod]
    public void WriteProducesDeterministicUniformTable()
    {
        var rows = new[]
        {
            new SampleRow("Alpha", true, 1, null),
            new SampleRow("42", false, 0, string.Empty)
        };
        var columns = new[]
        {
            new TabularColumn<SampleRow>(
                "name",
                row => TabularCell.From(row.Name)),
            new TabularColumn<SampleRow>(
                "active",
                row => TabularCell.From(row.Active)),
            new TabularColumn<SampleRow>(
                "count",
                row => TabularCell.From((long)row.Count)),
            new TabularColumn<SampleRow>(
                "note",
                row => TabularCell.From(row.Note))
        };
        var presentation = TabularPresentation.Create("items", rows, columns);

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "items[2]{name,active,count,note}:\n" +
            "  Alpha,true,1,null\n" +
            "  \"42\",false,0,\"\"",
            result);
        Assert.IsFalse(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WriteUsesCanonicalEmptyTableForm()
    {
        var presentation = CreateStringTable("items", Array.Empty<string?>());

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual("items: []", result);
    }

    [TestMethod]
    public void WriteAppliesToonStringQuotingRules()
    {
        var values = new string?[]
        {
            string.Empty,
            " leading",
            "trailing ",
            "true",
            "TRUE",
            "+1",
            ".5",
            "a,b",
            "a:b",
            "-item",
            "#item",
            "with[x]",
            "Hello 世界 👋"
        };
        var presentation = CreateStringTable("items", values);

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "items[13]{value}:\n" +
            "  \"\"\n" +
            "  \" leading\"\n" +
            "  \"trailing \"\n" +
            "  \"true\"\n" +
            "  TRUE\n" +
            "  \"+1\"\n" +
            "  .5\n" +
            "  \"a,b\"\n" +
            "  \"a:b\"\n" +
            "  \"-item\"\n" +
            "  \"#item\"\n" +
            "  \"with[x]\"\n" +
            "  Hello 世界 👋",
            result);
    }

    [TestMethod]
    public void WriteEscapesOnlyTheToonEscapeSet()
    {
        var value = "quote\" slash\\ line\ncarriage\rtab\tzero\0";
        var presentation = CreateStringTable("items", new[] { value });

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "items[1]{value}:\n" +
            "  \"quote\\\" slash\\\\ line\\ncarriage\\rtab\\tzero\\u0000\"",
            result);
    }

    [TestMethod]
    public void WriteQuotesUnsafeTableAndColumnNames()
    {
        var columns = new[]
        {
            new TabularColumn<string>(
                "display name",
                value => TabularCell.From(value))
        };
        var presentation = TabularPresentation.Create(
            "device-table",
            new[] { "value" },
            columns);

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "\"device-table\"[1]{\"display name\"}:\n  value",
            result);
    }

    [TestMethod]
    public void WriteDistinguishesNullFromEmptyString()
    {
        var presentation = CreateStringTable(
            "items",
            new string?[] { null, string.Empty });

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "items[2]{value}:\n  null\n  \"\"",
            result);
    }

    [TestMethod]
    public void WriteNormalisesDateTimeAsIso8601String()
    {
        var date = new DateTime(
            2026,
            7,
            29,
            8,
            10,
            0,
            DateTimeKind.Utc);
        var columns = new[]
        {
            new TabularColumn<DateTime>(
                "modifiedDate",
                value => TabularCell.From(value))
        };
        var presentation = TabularPresentation.Create(
            "items",
            new[] { date },
            columns);

        var result = ToonTableWriter.Write(presentation);

        Assert.AreEqual(
            "items[1]{modifiedDate}:\n" +
            "  \"2026-07-29T08:10:00.0000000Z\"",
            result);
    }

    [TestMethod]
    public void WriteRejectsUnpairedSurrogate()
    {
        var presentation = CreateStringTable(
            "items",
            new[] { "\ud800" });

        Assert.ThrowsException<InvalidOperationException>(
            () => ToonTableWriter.Write(presentation));
    }

    private static TabularPresentation CreateStringTable(
        string name,
        string?[] values)
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

        return TabularPresentation.Create(name, rows, columns);
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
        public SampleRow(string name, bool active, int count, string? note)
        {
            Name = name;
            Active = active;
            Count = count;
            Note = note;
        }

        public string Name { get; }

        public bool Active { get; }

        public int Count { get; }

        public string? Note { get; }
    }
}
