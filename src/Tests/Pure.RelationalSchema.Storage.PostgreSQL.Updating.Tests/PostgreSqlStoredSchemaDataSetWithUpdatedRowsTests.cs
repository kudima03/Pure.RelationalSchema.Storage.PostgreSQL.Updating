using Npgsql;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Insertion;
using Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests;

public sealed record PostgreSqlStoredSchemaDataSetWithUpdatedRowsTests
    : IAsyncLifetime,
        IDisposable
{
    private DatabaseFixture? _fixture;

    [Fact]
    public void UpdatesEveryRowMatchingPartialKey()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Age, "30")], [Cell(_fixture.Name, "Thirty")])
            )
        );

        Assert.Equal(
            ["1|Thirty|30|true", "2|Bob|40|true", "3|Thirty|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UpdatesRowMatchedByAllOfItsCells()
    {
        SeedUsers();
        IRow bob = User("2", "Bob", "40", "true");

        Touch(
            Updating(
                _fixture!.Users,
                new Update(bob.Cells, [Cell(_fixture.Age, "41")])
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|41|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void RowDifferingInOneKeyCellIsNotUpdated()
    {
        SeedUsers();
        IRow almostBob = User("2", "Bob", "40", "false");

        Touch(
            Updating(
                _fixture!.Users,
                new Update(almostBob.Cells, [Cell(_fixture.Age, "41")])
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void EmptyKeyUpdatesEveryRow()
    {
        SeedUsers();

        Touch(
            Updating(_fixture!.Users, new Update([], [Cell(_fixture.Active, "false")]))
        );

        Assert.Equal(
            ["1|Alice|30|false", "2|Bob|40|false", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void ChangesKeyColumn()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Id, "10")])
            )
        );

        Assert.Equal(
            ["10|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void LeavesColumnsOutsideOfSetUntouched()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update(
                    [Cell(_fixture.Id, "3")],
                    [Cell(_fixture.Name, "Caroline"), Cell(_fixture.Active, "true")]
                )
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Caroline|30|true"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UpdateMatchingNothingIsNoOp()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Id, "99")], [Cell(_fixture.Name, "Nobody")])
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void LaterUpdateOfSameRowWins()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "First")]),
                new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Second")])
            )
        );

        Assert.Equal(
            ["1|Second|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void LaterUpdateSeesEffectOfEarlierOne()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Id, "5")]),
                new Update([Cell(_fixture.Id, "5")], [Cell(_fixture.Name, "Moved")]),
                new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Lost")])
            )
        );

        Assert.Equal(
            ["2|Bob|40|true", "3|Carol|30|false", "5|Moved|30|true"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void SkipsUpdateWithEmptySet()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update([Cell(_fixture.Id, "1")], []),
                new Update([Cell(_fixture.Id, "2")], [Cell(_fixture.Name, "Robert")]),
                new Update([], [])
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Robert|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void ExecutesNothingWhenEverySetIsEmpty()
    {
        SeedUsers();

        Touch(Updating(_fixture!.Users, new Update([], []), new Update([], [])));

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void ExecutesNothingWithoutUpdates()
    {
        SeedUsers();

        Touch(Updating(_fixture!.Users));

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void MatchesAndWritesValuesWithSingleQuotes()
    {
        _fixture!.Seed(_fixture.Orders, ["1", "it's"], ["2", "its"]);

        Touch(
            Updating(
                _fixture.Orders,
                new Update(
                    [Cell(_fixture.Note, "it's")],
                    [Cell(_fixture.Note, "O'Reilly's ''quoted''")]
                )
            )
        );

        Assert.Equal(
            ["1|O'Reilly's ''quoted''", "2|its"],
            _fixture.Stored(_fixture.Orders)
        );
    }

    [Fact]
    public void MatchesAndWritesValuesWithBackslashes()
    {
        IPostgreSqlStoredSchemaDataSet stored = Stored();
        Touch(
            new PostgreSqlStoredSchemaDataSetWithInsertedRows(
                stored,
                [
                    new TableRows(
                        _fixture!.Orders,
                        [Order("1", @"C:\temp\n"), Order("2", "plain")]
                    ),
                ]
            )
        );

        Touch(
            Updating(
                _fixture.Orders,
                new Update([Cell(_fixture.Note, @"C:\temp\n")], [Cell(_fixture.Note, @"D:\")])
            )
        );

        Assert.Equal([@"1|D:\", "2|plain"], _fixture.Stored(_fixture.Orders));
    }

    [Fact]
    public void SameColumnTwiceInKeyRequiresBothValues()
    {
        SeedUsers();

        Touch(
            Updating(
                _fixture!.Users,
                new Update(
                    [Cell(_fixture.Id, "1"), Cell(_fixture.Id, "2")],
                    [Cell(_fixture.Name, "Nobody")]
                ),
                new Update(
                    [Cell(_fixture.Id, "3"), Cell(_fixture.Id, "3")],
                    [Cell(_fixture.Name, "Caroline")]
                )
            )
        );

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Caroline|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void SameColumnTwiceInSetFailsAndRollsBackTable()
    {
        SeedUsers();
        IPostgreSqlStoredSchemaDataSet dataSet = Updating(
            _fixture!.Users,
            new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Changed")]),
            new Update(
                [Cell(_fixture.Id, "2")],
                [Cell(_fixture.Name, "A"), Cell(_fixture.Name, "B")]
            )
        );

        PostgresException exception = Assert.Throws<PostgresException>(() =>
            Touch(dataSet)
        );

        Assert.Equal(PostgresErrorCodes.SyntaxError, exception.SqlState);
        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void ColumnOutsideOfTableFailsAndRollsBackTable()
    {
        SeedUsers();
        IPostgreSqlStoredSchemaDataSet dataSet = Updating(
            _fixture!.Users,
            new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Changed")]),
            new Update([Cell(_fixture.Id, "2")], [Cell(_fixture.Note, "Order note")])
        );

        PostgresException exception = Assert.Throws<PostgresException>(() =>
            Touch(dataSet)
        );

        Assert.Equal(PostgresErrorCodes.UndefinedColumn, exception.SqlState);
        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UniqueIndexViolationFailsAndRollsBackTable()
    {
        SeedUsers();
        IPostgreSqlStoredSchemaDataSet dataSet = Updating(
            _fixture!.Users,
            new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Changed")]),
            new Update([Cell(_fixture.Id, "2")], [Cell(_fixture.Id, "3")])
        );

        PostgresException exception = Assert.Throws<PostgresException>(() =>
            Touch(dataSet)
        );

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void DoesNotUpdateBeforeFirstAccess()
    {
        SeedUsers();

        _ = Updating(_fixture!.Users, new Update([], [Cell(_fixture.Name, "Same")]));

        Assert.Equal(
            ["1|Alice|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UpdatesOnlyOnce()
    {
        SeedUsers();
        IPostgreSqlStoredSchemaDataSet dataSet = Updating(
            _fixture!.Users,
            new Update([Cell(_fixture.Age, "30")], [Cell(_fixture.Age, "31")])
        );

        _ = dataSet[_fixture.Users].TableSchema;
        _fixture.Seed(_fixture.Users, ["4", "Dave", "30", "true"]);
        _ = dataSet[_fixture.Users].ElementType;
        _ = dataSet[_fixture.Users].AsEnumerable().Count();

        Assert.Equal(
            ["1|Alice|31|true", "2|Bob|40|true", "3|Carol|31|false", "4|Dave|30|true"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UpdatesEverySuppliedTable()
    {
        SeedUsers();
        _fixture!.Seed(_fixture.Orders, ["1", "first"], ["2", "second"]);

        Touch(
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                Stored(),
                [
                    new TableUpdates(
                        _fixture.Orders,
                        new Update([Cell(_fixture.OrderId, "2")], [Cell(_fixture.Note, "2nd")])
                    ),
                    new TableUpdates(
                        _fixture.Users,
                        new Update([Cell(_fixture.Id, "2")], [Cell(_fixture.Name, "Robert")])
                    ),
                ]
            )
        );

        Assert.Equal(["1|first", "2|2nd"], _fixture.Stored(_fixture.Orders));
        Assert.Equal(
            ["1|Alice|30|true", "2|Robert|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void UpdatesNothingInTablesOutsideOfGroupings()
    {
        SeedUsers();
        _fixture!.Seed(_fixture.Orders, ["1", "first"]);

        Touch(Updating(_fixture.Users, new Update([], [Cell(_fixture.Name, "Same")])));

        Assert.Equal(["1|first"], _fixture.Stored(_fixture.Orders));
        Assert.Equal(
            ["1|Same|30|true", "2|Same|40|true", "3|Same|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    [Fact]
    public void WrappingTwiceAppliesInnerUpdatesFirst()
    {
        SeedUsers();

        Touch(
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                Updating(
                    _fixture!.Users,
                    new Update([Cell(_fixture.Id, "1")], [Cell(_fixture.Name, "Inner")])
                ),
                [
                    new TableUpdates(
                        _fixture.Users,
                        new Update(
                            [Cell(_fixture.Name, "Inner")],
                            [Cell(_fixture.Name, "Outer")]
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            ["1|Outer|30|true", "2|Bob|40|true", "3|Carol|30|false"],
            _fixture.Stored(_fixture.Users)
        );
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public Task InitializeAsync()
    {
        _fixture = new DatabaseFixture();
        return Task.CompletedTask;
    }

    private static void Touch(IPostgreSqlStoredSchemaDataSet dataSet)
    {
        foreach (IStoredTableDataSet tableDataSet in dataSet.Values)
        {
            _ = tableDataSet.TableSchema;
        }
    }

    private static KeyValuePair<IColumn, ICell> Cell(IColumn column, string value)
    {
        return new KeyValuePair<IColumn, ICell>(column, new Cell(new String(value)));
    }

    private void SeedUsers()
    {
        _fixture!.Seed(
            _fixture.Users,
            ["1", "Alice", "30", "true"],
            ["2", "Bob", "40", "true"],
            ["3", "Carol", "30", "false"]
        );
    }

    private IRow User(string id, string name, string age, string active)
    {
        return new Row(
            new Dictionary<KeyValuePair<IColumn, ICell>, IColumn, ICell>(
                [
                    Cell(_fixture!.Id, id),
                    Cell(_fixture.Name, name),
                    Cell(_fixture.Age, age),
                    Cell(_fixture.Active, active),
                ],
                x => x.Key,
                x => x.Value,
                x => new ColumnHash(x)
            )
        );
    }

    private IRow Order(string id, string note)
    {
        return new Row(
            new Dictionary<KeyValuePair<IColumn, ICell>, IColumn, ICell>(
                [Cell(_fixture!.OrderId, id), Cell(_fixture.Note, note)],
                x => x.Key,
                x => x.Value,
                x => new ColumnHash(x)
            )
        );
    }

    private PostgreSqlStoredSchemaDataSetWithUpdatedRows Updating(
        ITable table,
        params IEnumerable<
            IGrouping<
                IEnumerable<KeyValuePair<IColumn, ICell>>,
                KeyValuePair<IColumn, ICell>
            >
        > updates
    )
    {
        return new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
            Stored(),
            [new TableUpdates(table, updates)]
        );
    }

    private FakeStoredSchemaDataSet Stored()
    {
        return new FakeStoredSchemaDataSet(
            _fixture!.Schema,
            _fixture.Connection,
            _ => []
        );
    }
}
