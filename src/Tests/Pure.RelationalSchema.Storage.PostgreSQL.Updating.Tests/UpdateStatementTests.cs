using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests;

using Column = Column.Column;
using Table = Table.Table;

public sealed record UpdateStatementTests
{
    private static readonly string TableName = new string('t', 64);

    private static readonly string SchemaName = new string('s', 64);

    private static readonly IColumn First = new Column(
        new String("First"),
        new StringColumnType()
    );

    private static readonly IColumn Second = new Column(
        new String("Second"),
        new IntColumnType()
    );

    private static readonly IColumn Third = new Column(
        new String("Third"),
        new BoolColumnType()
    );

    [Fact]
    public void ProducesUpdateWithSetAndWhere()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "a"), Cell(Second, "1")],
            [Cell(Third, "true"), Cell(Second, "2")]
        );

        Assert.Equal(
            $"UPDATE \"{Trimmed(SchemaName)}\".\"{Trimmed(TableName)}\" SET "
                + $"\"{Hash(Third)}\" = 'true', \"{Hash(Second)}\" = '2' WHERE "
                + $"\"{Hash(First)}\" = 'a' AND \"{Hash(Second)}\" = '1';",
            statement.TextValue
        );
    }

    [Fact]
    public void ProducesSingleConditionForSingleKeyCell()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "a")],
            [Cell(Third, "true")]
        );

        Assert.EndsWith(
            $"SET \"{Hash(Third)}\" = 'true' WHERE \"{Hash(First)}\" = 'a';",
            statement.TextValue
        );
    }

    [Fact]
    public void OmitsWhereForEmptyKey()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [],
            [Cell(Third, "false")]
        );

        Assert.Equal(
            $"UPDATE \"{Trimmed(SchemaName)}\".\"{Trimmed(TableName)}\" SET "
                + $"\"{Hash(Third)}\" = 'false';",
            statement.TextValue
        );
    }

    [Fact]
    public void KeepsSuppliedOrderOfCells()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(Second, "1"), Cell(First, "a")],
            [Cell(Second, "2"), Cell(First, "b")]
        );

        Assert.EndsWith(
            $"SET \"{Hash(Second)}\" = '2', \"{Hash(First)}\" = 'b' WHERE "
                + $"\"{Hash(Second)}\" = '1' AND \"{Hash(First)}\" = 'a';",
            statement.TextValue
        );
    }

    [Fact]
    public void RendersRepeatedColumnAsIs()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "a"), Cell(First, "b")],
            [Cell(Second, "1"), Cell(Second, "2")]
        );

        Assert.EndsWith(
            $"SET \"{Hash(Second)}\" = '1', \"{Hash(Second)}\" = '2' WHERE "
                + $"\"{Hash(First)}\" = 'a' AND \"{Hash(First)}\" = 'b';",
            statement.TextValue
        );
    }

    [Fact]
    public void EscapesSingleQuotesInValues()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "it's"), Cell(Second, "'")],
            [Cell(First, "''"), Cell(Second, "a'b'c")]
        );

        Assert.EndsWith(
            $"SET \"{Hash(First)}\" = '''''', \"{Hash(Second)}\" = 'a''b''c' WHERE "
                + $"\"{Hash(First)}\" = 'it''s' AND \"{Hash(Second)}\" = '''';",
            statement.TextValue
        );
    }

    [Fact]
    public void LeavesBackslashesAsTheyAre()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [],
            [Cell(First, @"C:\temp\n")]
        );

        Assert.EndsWith($"\"{Hash(First)}\" = 'C:\\temp\\n';", statement.TextValue);
    }

    [Fact]
    public void TrimsIdentifiersToPostgreSqlLimit()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(new string('t', 100)),
            new String(new string('s', 100)),
            [Cell(First, "a")],
            [Cell(Second, "1")]
        );

        Assert.StartsWith(
            $"UPDATE \"{new string('s', 63)}\".\"{new string('t', 63)}\" SET",
            statement.TextValue
        );
    }

    [Fact]
    public void UsesHashOfTableAndKeyAndCellsOfUpdate()
    {
        ITable table = new Table(new String("Test"), [First, Second], []);

        UpdateStatement statement = new UpdateStatement(
            table,
            new String(SchemaName),
            new Update([Cell(First, "a")], [Cell(Second, "1")])
        );

        Assert.Equal(
            $"UPDATE \"{Trimmed(SchemaName)}\"."
                + $"\"{Hash(new TableHash(table))}\" SET "
                + $"\"{Hash(Second)}\" = '1' WHERE \"{Hash(First)}\" = 'a';",
            statement.TextValue
        );
    }

    [Fact]
    public void EnumeratesCharactersOfText()
    {
        UpdateStatement statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "a")],
            [Cell(Second, "1")]
        );

        Assert.Equal(statement.TextValue, Text(statement));
    }

    [Fact]
    public void EnumeratesCharactersOfTextNonGenerically()
    {
        IEnumerable statement = new UpdateStatement(
            new String(TableName),
            new String(SchemaName),
            [Cell(First, "a")],
            [Cell(Second, "1")]
        );

        Assert.Equal(
            ((UpdateStatement)statement).TextValue,
            Text(new NonGenericItems(statement).Cast<IChar>())
        );
    }

    [Fact]
    public void CellEqualityComparesColumnWithQuotedValue()
    {
        CellEquality equality = new CellEquality(Cell(First, "it's"));

        Assert.Equal($"\"{Hash(First)}\" = 'it''s'", equality.TextValue);
    }

    [Fact]
    public void CellEqualityEnumeratesCharactersOfText()
    {
        CellEquality equality = new CellEquality(Cell(First, "a"));

        Assert.Equal(equality.TextValue, Text(equality));
    }

    [Fact]
    public void CellEqualityEnumeratesCharactersOfTextNonGenerically()
    {
        IEnumerable equality = new CellEquality(Cell(First, "a"));

        Assert.Equal(
            ((CellEquality)equality).TextValue,
            Text(new NonGenericItems(equality).Cast<IChar>())
        );
    }

    [Fact]
    public void EscapedStringEnumeratesCharactersOfText()
    {
        IEnumerable escaped = new SingleQuoteEscapedString(new String("it's"));

        Assert.Equal("it''s", Text(new NonGenericItems(escaped).Cast<IChar>()));
    }

    [Fact]
    public void EscapedStringEnumeratesCharactersOfTextGenerically()
    {
        SingleQuoteEscapedString escaped = new SingleQuoteEscapedString(new String("'"));

        Assert.Equal("''", Text(escaped));
    }

    [Fact]
    public void TrimmedHashEnumeratesCharactersOfText()
    {
        IEnumerable trimmed = new TrimmedHash(new String(new string('x', 70)));

        Assert.Equal(
            new string('x', 63),
            Text(new NonGenericItems(trimmed).Cast<IChar>())
        );
    }

    [Fact]
    public void TrimmedHashEnumeratesCharactersOfTextGenerically()
    {
        TrimmedHash trimmed = new TrimmedHash(new String(new string('y', 63)));

        Assert.Equal(new string('y', 63), Text(trimmed));
    }

    private static string Text(IEnumerable<IChar> chars)
    {
        return new string([.. chars.Select(x => x.CharValue)]);
    }

    private static string Trimmed(string name)
    {
        return name[..63];
    }

    private static string Hash(IColumn column)
    {
        return Hash(new ColumnHash(column));
    }

    private static string Hash(Pure.HashCodes.Abstractions.IDeterminedHash hash)
    {
        string hex = new HexString(hash).TextValue;
        return hex[..Math.Min(63, hex.Length)];
    }

    private static KeyValuePair<IColumn, ICell> Cell(IColumn column, string value)
    {
        return new KeyValuePair<IColumn, ICell>(column, new Cell(new String(value)));
    }
}
