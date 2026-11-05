using System.Data;
using Npgsql;
using Pure.HashCodes.Abstractions;
using Pure.Primitives.Bool;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Testcontainers.PostgreSql;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests;

using Column = Column.Column;
using Index = Index.Index;
using Schema = Schema.Schema;
using Table = Table.Table;

public sealed record DatabaseFixture : IDisposable
{
    private const int MaxIdentifierLength = 63;

    private readonly PostgreSqlContainer _postgres;

    public IDbConnection Connection { get; }

    public ISchema Schema { get; }

    public ITable Users { get; }

    public ITable Orders { get; }

    public IColumn Id { get; }

    public IColumn Name { get; }

    public IColumn Age { get; }

    public IColumn Active { get; }

    public IColumn OrderId { get; }

    public IColumn Note { get; }

    public DatabaseFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();

        Id = new Column(new String("Id"), new IntColumnType());
        Name = new Column(new String("Name"), new StringColumnType());
        Age = new Column(new String("Age"), new IntColumnType());
        Active = new Column(new String("Active"), new BoolColumnType());
        OrderId = new Column(new String("OrderId"), new IntColumnType());
        Note = new Column(new String("Note"), new StringColumnType());

        Users = new Table(
            new String("Users"),
            [Id, Name, Age, Active],
            [new Index(new True(), [Id])]
        );

        Orders = new Table(new String("Orders"), [OrderId, Note], []);

        Schema = new Schema(new String("Test"), [Users, Orders], []);

        Execute(
            $"CREATE SCHEMA IF NOT EXISTS \"{Trimmed(new SchemaHash(Schema))}\";"
        );

        foreach (ITable table in Schema.Tables)
        {
            Execute(
                $"CREATE TABLE IF NOT EXISTS {QualifiedName(table)} ("
                    + string.Join(
                        ", ",
                        table.Columns.Select(x =>
                            $"\"{Trimmed(new ColumnHash(x))}\" {TypeName(x.Type)} NOT NULL"
                        )
                    )
                    + ");"
            );
        }

        Execute(
            $"CREATE UNIQUE INDEX ON {QualifiedName(Users)} "
                + $"(\"{Trimmed(new ColumnHash(Id))}\");"
        );
    }

    public void Seed(ITable table, params IEnumerable<IEnumerable<string>> rows)
    {
        foreach (IEnumerable<string> row in rows)
        {
            Execute(
                $"INSERT INTO {QualifiedName(table)} VALUES ("
                    + string.Join(", ", row.Select(x => $"'{x.Replace("'", "''")}'"))
                    + ");"
            );
        }
    }

    public IEnumerable<string> Stored(ITable table)
    {
        using IDbCommand command = Connection.CreateCommand();
        command.CommandText =
            "SELECT concat_ws('|', "
            + string.Join(
                ", ",
                table.Columns.Select(x => $"\"{Trimmed(new ColumnHash(x))}\"::text")
            )
            + $") AS row FROM {QualifiedName(table)} ORDER BY row;";
        using IDataReader reader = command.ExecuteReader();
        List<string> rows = [];
        while (reader.Read())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static string Trimmed(IDeterminedHash hash)
    {
        string name = new HexString(hash).TextValue;
        return name[..Math.Min(MaxIdentifierLength, name.Length)];
    }

    private static string TypeName(IColumnType type)
    {
        return type switch
        {
            BoolColumnType => "boolean",
            IntColumnType => "integer",
            StringColumnType => "text",
            _ => throw new NotSupportedException(),
        };
    }

    private string QualifiedName(ITable table)
    {
        return $"\"{Trimmed(new SchemaHash(Schema))}\".\"{Trimmed(new TableHash(table))}\"";
    }

    private void Execute(string sql)
    {
        using IDbCommand command = Connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }
}
