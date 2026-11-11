using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcp.Contracts;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class TabularPresentationTests
{
    [TestMethod]
    public void CreateUsesExplicitColumnOrderAndMaterialisesRows()
    {
        var source = new[]
        {
            new SampleRow("First", 1, true),
            new SampleRow("Second", 2, null)
        };
        var columns = new[]
        {
            new TabularColumn<SampleRow>(
                "count",
                row => TabularCell.From((long)row.Count)),
            new TabularColumn<SampleRow>(
                "name",
                row => TabularCell.From(row.Name)),
            new TabularColumn<SampleRow>(
                "active",
                row => TabularCell.From(row.Active))
        };

        var presentation = TabularPresentation.Create("items", source, columns);

        CollectionAssert.AreEqual(
            new[] { "count", "name", "active" },
            presentation.Columns.ToArray());
        Assert.AreEqual(2, presentation.Rows.Count);
        Assert.AreEqual(TabularCell.From(1L), presentation.Rows[0][0]);
        Assert.AreEqual(TabularCell.From("First"), presentation.Rows[0][1]);
        Assert.AreEqual(TabularCell.Null, presentation.Rows[1][2]);
    }

    [TestMethod]
    public void CreateCopiesColumnDefinitions()
    {
        var columns = new List<TabularColumn<SampleRow>>
        {
            new TabularColumn<SampleRow>(
                "name",
                row => TabularCell.From(row.Name))
        };
        var presentation = TabularPresentation.Create(
            "items",
            new[] { new SampleRow("First", 1, true) },
            columns);

        columns[0] = new TabularColumn<SampleRow>(
            "replacement",
            row => TabularCell.From(row.Name));

        Assert.AreEqual("name", presentation.Columns[0]);
    }

    [TestMethod]
    public void CreateAcceptsMaximumRowCount()
    {
        var rows = Enumerable.Range(0, TabularPresentation.MaximumRows);
        var columns = new[]
        {
            new TabularColumn<int>(
                "value",
                value => TabularCell.From((long)value))
        };

        var presentation = TabularPresentation.Create("items", rows, columns);

        Assert.AreEqual(TabularPresentation.MaximumRows, presentation.Rows.Count);
    }

    [TestMethod]
    public void CreateRejectsMoreThanMaximumRows()
    {
        var rows = Enumerable.Range(0, TabularPresentation.MaximumRows + 1);
        var columns = new[]
        {
            new TabularColumn<int>(
                "value",
                value => TabularCell.From((long)value))
        };

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => TabularPresentation.Create("items", rows, columns));
    }

    [TestMethod]
    public void CreateRejectsMoreThanMaximumColumns()
    {
        var columns = Enumerable
            .Range(0, TabularPresentation.MaximumColumns + 1)
            .Select(index => new TabularColumn<int>(
                $"column{index}",
                value => TabularCell.From((long)value)))
            .ToArray();

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => TabularPresentation.Create("items", new[] { 1 }, columns));
    }

    [TestMethod]
    public void CreateRejectsMissingAndDuplicateColumns()
    {
        Assert.ThrowsException<ArgumentException>(
            () => TabularPresentation.Create(
                "items",
                new[] { 1 },
                Array.Empty<TabularColumn<int>>()));

        var duplicateColumns = new[]
        {
            new TabularColumn<int>(
                "value",
                value => TabularCell.From((long)value)),
            new TabularColumn<int>(
                "value",
                value => TabularCell.From((long)value))
        };

        Assert.ThrowsException<ArgumentException>(
            () => TabularPresentation.Create(
                "items",
                new[] { 1 },
                duplicateColumns));
    }

    [TestMethod]
    public void CreateRejectsNullRows()
    {
        var rows = new[] { (string)null! };
        var columns = new[]
        {
            new TabularColumn<string>(
                "value",
                value => TabularCell.From(value))
        };

        Assert.ThrowsException<ArgumentException>(
            () => TabularPresentation.Create("items", rows, columns));
    }

    [TestMethod]
    public void ColumnRequiresANameAndSelector()
    {
        Assert.ThrowsException<ArgumentException>(
            () => new TabularColumn<int>(
                " ",
                value => TabularCell.From((long)value)));

        Assert.ThrowsException<ArgumentNullException>(
            () => new TabularColumn<int>("value", null!));
    }

    private sealed class SampleRow
    {
        public SampleRow(string name, int count, bool? active)
        {
            Name = name;
            Count = count;
            Active = active;
        }

        public string Name { get; }

        public int Count { get; }

        public bool? Active { get; }
    }
}
