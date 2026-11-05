using System.Collections;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests;

public sealed record PostgreSqlStoredSchemaDataSetWithUpdatedRowsMembersTests
    : IAsyncLifetime,
        IDisposable
{
    private const int ExistingRowsCount = 3;

    private DatabaseFixture? _fixture;

    [Fact]
    public void CountEqualsNumberOfSuppliedGroupings()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates(schema.Keys));

        Assert.Equal(schema.Count, dataSet.Count);
    }

    [Fact]
    public void ExposesOnlyTablesFromSuppliedGroupings()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable included = schema.Keys.First();
        ITable excluded = schema.Keys.Last();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([included]));

        _ = Assert.Single(dataSet);
        Assert.Equal([included], dataSet.Keys);
        _ = Assert.Single(dataSet.Values);
        Assert.True(dataSet.ContainsKey(included));
        Assert.False(dataSet.ContainsKey(excluded));
        _ = Assert.ThrowsAny<Exception>(() => dataSet[excluded]);
    }

    [Fact]
    public void KeysAndValuesFollowSuppliedOrder()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                schema,
                Updates(schema.Keys.Reverse())
            );

        Assert.Equal(schema.Keys.Reverse(), dataSet.Keys);
        Assert.Equal(dataSet.Count, dataSet.Values.Count());
        Assert.All(
            dataSet.Values.Zip(dataSet.Select(x => x.Value)),
            pair => Assert.Same(pair.First, pair.Second)
        );
    }

    [Fact]
    public void TryGetValueFindsSuppliedTable()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        Assert.True(dataSet.TryGetValue(table, out IStoredTableDataSet? value));
        Assert.Same(dataSet[table], value);
    }

    [Fact]
    public void TryGetValueDoesNotFindMissingTable()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                schema,
                Updates([schema.Keys.First()])
            );

        Assert.False(dataSet.TryGetValue(schema.Keys.Last(), out IStoredTableDataSet? _));
    }

    [Fact]
    public void ExposesConnectionAndSchemaOfWrappedDataSet()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates(schema.Keys));

        Assert.Same(schema.Connection, dataSet.Connection);
        Assert.Same(schema.Schema, dataSet.Schema);
    }

    [Fact]
    public void NonGenericEnumerationYieldsAllPairs()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();

        IEnumerable dataSet = new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
            schema,
            Updates(schema.Keys)
        );

        Assert.Equal(schema.Count, new NonGenericItems(dataSet).Count());
    }

    [Fact]
    public void TableDataSetExposesWrappedTableSchema()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        Assert.Same(schema[table].TableSchema, dataSet[table].TableSchema);
    }

    [Fact]
    public void TableDataSetExposesWrappedElementType()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        Assert.Equal(schema[table].ElementType, dataSet[table].ElementType);
    }

    [Fact]
    public void TableDataSetExposesWrappedQueryableMembers()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        IStoredTableDataSet tableDataSet = dataSet[table];

        Assert.Same(schema[table].Expression, tableDataSet.Expression);
        Assert.Same(schema[table].Provider, tableDataSet.Provider);
        Assert.Equal(ExistingRowsCount, tableDataSet.AsQueryable().Count());
    }

    [Fact]
    public void TableDataSetEnumeratesWrappedRows()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        Assert.Equal(ExistingRowsCount, dataSet[table].AsEnumerable().Count());
    }

    [Fact]
    public void TableDataSetNonGenericEnumerationYieldsWrappedRows()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        IEnumerable tableDataSet = dataSet[table];

        Assert.Equal(ExistingRowsCount, new NonGenericItems(tableDataSet).Count());
    }

    [Fact]
    public async Task TableDataSetEnumeratesWrappedRowsAsynchronously()
    {
        IPostgreSqlStoredSchemaDataSet schema = Schema();
        ITable table = schema.Keys.First();

        IPostgreSqlStoredSchemaDataSet dataSet =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(schema, Updates([table]));

        IRow[] rows = await dataSet[table].ToArrayAsync();

        Assert.Equal(ExistingRowsCount, rows.Length);
    }

    [Fact]
    public async Task AsynchronousEnumerationUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = await dataSet[_fixture!.Users].ToArrayAsync();

        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void SynchronousEnumerationUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        Assert.Equal(ExistingRowsCount, dataSet[_fixture!.Users].AsEnumerable().Count());
        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void NonGenericEnumerationUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        IEnumerable tableDataSet = dataSet[_fixture!.Users];

        Assert.Equal(ExistingRowsCount, new NonGenericItems(tableDataSet).Count());
        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void ProviderUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = dataSet[_fixture!.Users].Provider;

        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void ExpressionUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = dataSet[_fixture!.Users].Expression;

        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void ElementTypeUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = dataSet[_fixture!.Users].ElementType;

        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void TableSchemaUpdatesRows()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = dataSet[_fixture!.Users].TableSchema;

        Assert.Equal(["1|Renamed|30|true"], _fixture.Stored(_fixture.Users));
    }

    [Fact]
    public void ConnectionIsNotDisposed()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = RenamingEveryUser();

        _ = dataSet[_fixture!.Users].TableSchema;

        Assert.Equal(System.Data.ConnectionState.Open, dataSet.Connection.State);
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

    private static IEnumerable<TableUpdates> Updates(IEnumerable<ITable> tables)
    {
        return tables.Select(table => new TableUpdates(
            table,
            new Update(
                [],
                [
                    new KeyValuePair<IColumn, ICell>(
                        table.Columns.First(),
                        new Cell(new String("7"))
                    ),
                ]
            )
        ));
    }

    private IPostgreSqlStoredSchemaDataSet RenamingEveryUser()
    {
        _fixture!.Seed(_fixture.Users, ["1", "Alice", "30", "true"]);
        return new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
            Schema(),
            [
                new TableUpdates(
                    _fixture.Users,
                    new Update(
                        [],
                        [
                            new KeyValuePair<IColumn, ICell>(
                                _fixture.Name,
                                new Cell(new String("Renamed"))
                            ),
                        ]
                    )
                ),
            ]
        );
    }

    private FakeStoredSchemaDataSet Schema()
    {
        return new FakeStoredSchemaDataSet(
            _fixture!.Schema,
            _fixture.Connection,
            table =>
                Enumerable
                    .Range(0, ExistingRowsCount)
                    .Select(_ => new Row(
                        new Dictionary<IColumn, IColumn, ICell>(
                            table.Columns,
                            column => column,
                            column => new Cell(new String("1")),
                            column => new ColumnHash(column)
                        )
                    ))
        );
    }
}
