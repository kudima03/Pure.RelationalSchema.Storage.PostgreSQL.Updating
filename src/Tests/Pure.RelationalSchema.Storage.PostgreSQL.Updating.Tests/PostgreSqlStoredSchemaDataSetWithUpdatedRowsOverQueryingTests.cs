using Npgsql;
using Pure.Collections.Generic;
using Pure.Primitives.Bool;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Creation;
using Pure.RelationalSchema.Storage.PostgreSQL.Insertion;
using Pure.RelationalSchema.Storage.PostgreSQL.Querying;
using Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests;

using Column = Column.Column;
using ForeignKey = ForeignKey.ForeignKey;
using Index = Index.Index;
using Schema = Schema.Schema;
using Table = Table.Table;

public sealed record PostgreSqlStoredSchemaDataSetWithUpdatedRowsOverQueryingTests
    : IAsyncLifetime,
        IDisposable
{
    private readonly IColumn _userId = new Column(new String("Id"), new IntColumnType());

    private readonly IColumn _userName = new Column(
        new String("Name"),
        new StringColumnType()
    );

    private readonly IColumn _orderId = new Column(new String("Id"), new IntColumnType());

    private readonly IColumn _orderUser = new Column(
        new String("UserId"),
        new IntColumnType()
    );

    private readonly ITable _users;

    private readonly ITable _orders;

    private readonly ISchema _schema;

    private DatabaseFixture? _fixture;

    public PostgreSqlStoredSchemaDataSetWithUpdatedRowsOverQueryingTests()
    {
        _users = new Table(
            new String("Users"),
            [_userId, _userName],
            [new Index(new True(), [_userId])]
        );
        _orders = new Table(new String("Orders"), [_orderId, _orderUser], []);
        _schema = new Schema(
            new String("Shop"),
            [_users, _orders],
            [new ForeignKey(_orders, [_orderUser], _users, [_userId])]
        );
    }

    [Fact]
    public void ReadsUpdatedRowsBackThroughQueryingDataSet()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = Seeded();

        IPostgreSqlStoredSchemaDataSet withUpdated =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                dataSet,
                [
                    new TableUpdates(
                        _users,
                        new Update([Cell(_userId, "1")], [Cell(_userName, "Alicia")]),
                        new Update([Cell(_userId, "3")], [Cell(_userName, "Nobody")])
                    ),
                ]
            );

        Assert.Equal(
            ["Alicia", "Bob O'Brien"],
            withUpdated[_users]
                .AsEnumerable()
                .Select(row => row.Cells[_userName].Value.TextValue)
                .Order()
        );
    }

    [Fact]
    public void ForeignKeyViolationFailsAndRollsBackTable()
    {
        IPostgreSqlStoredSchemaDataSet dataSet = Seeded();

        IPostgreSqlStoredSchemaDataSet withUpdated =
            new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
                dataSet,
                [
                    new TableUpdates(
                        _orders,
                        new Update([Cell(_orderId, "10")], [Cell(_orderUser, "2")]),
                        new Update([Cell(_orderId, "11")], [Cell(_orderUser, "99")])
                    ),
                ]
            );

        PostgresException exception = Assert.Throws<PostgresException>(() =>
            withUpdated[_orders].TableSchema
        );

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal(
            ["1", "1"],
            dataSet[_orders]
                .AsEnumerable()
                .Select(row => row.Cells[_orderUser].Value.TextValue)
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

    private static KeyValuePair<IColumn, ICell> Cell(IColumn column, string value)
    {
        return new KeyValuePair<IColumn, ICell>(column, new Cell(new String(value)));
    }

    private static IRow Row(params IEnumerable<KeyValuePair<IColumn, ICell>> cells)
    {
        return new Row(
            new Dictionary<KeyValuePair<IColumn, ICell>, IColumn, ICell>(
                cells,
                x => x.Key,
                x => x.Value,
                x => new ColumnHash(x)
            )
        );
    }

    private IPostgreSqlStoredSchemaDataSet Seeded()
    {
        ISchema created = new PostgreSqlCreatedSchema(_schema, _fixture!.Connection);
        _ = created.Tables.ToArray();

        IPostgreSqlStoredSchemaDataSet dataSet = new PostgreSqlStoredSchemaDataSet(
            created,
            _fixture.Connection
        );

        IPostgreSqlStoredSchemaDataSet withInserted =
            new PostgreSqlStoredSchemaDataSetWithInsertedRows(
                dataSet,
                [
                    new TableRows(
                        _users,
                        [
                            Row(Cell(_userId, "1"), Cell(_userName, "Alice")),
                            Row(Cell(_userId, "2"), Cell(_userName, "Bob O'Brien")),
                        ]
                    ),
                    new TableRows(
                        _orders,
                        [
                            Row(Cell(_orderId, "10"), Cell(_orderUser, "1")),
                            Row(Cell(_orderId, "11"), Cell(_orderUser, "1")),
                        ]
                    ),
                ]
            );

        _ = withInserted[_users].TableSchema;
        _ = withInserted[_orders].TableSchema;

        return dataSet;
    }
}
