using System.Collections.Generic;
using System.Text.Json;
using TiaMcp.Contracts;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class OutputBudgetTests
{
    [TestMethod]
    public void RepresentativeFlatTableCharacterCountKeepsCsvBelowToonBelowJson()
    {
        var rows = new List<SampleRow>();
        for (var index = 0; index < 50; index++)
        {
            rows.Add(
                new SampleRow(
                    $@"Project\PLC_1\Program blocks\Block_{index:D3}",
                    $"Block_{index:D3}",
                    "FunctionBlock",
                    index % 3 != 0));
        }

        var columns = new[]
        {
            new TabularColumn<SampleRow>(
                "path",
                row => TabularCell.From(row.Path)),
            new TabularColumn<SampleRow>(
                "name",
                row => TabularCell.From(row.Name)),
            new TabularColumn<SampleRow>(
                "typeName",
                row => TabularCell.From(row.TypeName)),
            new TabularColumn<SampleRow>(
                "isConsistent",
                row => TabularCell.From(row.IsConsistent))
        };
        var presentation = TabularPresentation.Create("items", rows, columns);

        var csv = CsvTableWriter.Write(presentation);
        var toon = ToonTableWriter.Write(presentation);
        var compactJson = JsonSerializer.Serialize(new { items = rows });

        Assert.IsTrue(
            csv.Length < toon.Length,
            $"Expected CSV ({csv.Length} characters) to be smaller than TOON " +
            $"({toon.Length} characters).");
        Assert.IsTrue(
            toon.Length < compactJson.Length,
            $"Expected TOON ({toon.Length} characters) to be smaller than " +
            $"compact JSON ({compactJson.Length} characters).");
    }

    private sealed class SampleRow
    {
        public SampleRow(
            string path,
            string name,
            string typeName,
            bool isConsistent)
        {
            Path = path;
            Name = name;
            TypeName = typeName;
            IsConsistent = isConsistent;
        }

        public string Path { get; }

        public string Name { get; }

        public string TypeName { get; }

        public bool IsConsistent { get; }
    }
}
