# Pure.RelationalSchema.Storage.PostgreSQL.Updating

PostgreSQL row updating for the **Pure** relational schema ecosystem — `UPDATE` decorators over `Pure.RelationalSchema.Storage.PostgreSQL.Querying` datasets.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Updating/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Updating/actions/workflows/build-and-test.yml)
[![publish NuGet](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Updating/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Updating/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PostgreSQL.Updating)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PostgreSQL.Updating)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PostgreSQL.Updating` updates rows in a live PostgreSQL database through the storage abstractions defined in `Pure.RelationalSchema.Storage`. `PostgreSqlStoredSchemaDataSetWithUpdatedRows` wraps any `IPostgreSqlStoredSchemaDataSet` (the usual implementation is `PostgreSqlStoredSchemaDataSet` from `Pure.RelationalSchema.Storage.PostgreSQL.Querying`) and a set of updates grouped by table. The updates of a table are executed lazily, the first time a table dataset of the result is accessed. After that, the result exposes the wrapped dataset, so the updated rows can be read back through the same `IStoredTableDataSet`.

Each update is an `IGrouping` of cells: its **key** is the set of cells that identifies the stored rows, and its **elements** are the cells to set. A set of cells is an `IEnumerable<KeyValuePair<IColumn, ICell>>`, so the `Cells` of an `IRow` can be passed as a key as they are. Every update becomes one statement:

```sql
UPDATE "<schema>"."<table>" SET "<c3>" = 'v3', "<c4>" = 'v4' WHERE "<c1>" = 'k1' AND "<c2>" = 'k2';
```

and a key without cells drops the `WHERE`:

```sql
UPDATE "<schema>"."<table>" SET "<c3>" = 'v3';
```

All statements of a table are sent together as one command, in the supplied order:

```sql
UPDATE "<schema>"."<table>" SET "<c2>" = 'Alicia' WHERE "<c1>" = '1'; UPDATE "<schema>"."<table>" SET "<c2>" = 'Robert' WHERE "<c1>" = '2';
```

Every column is created `NOT NULL`, so the comparisons are exact. Matching works as follows:

- A stored row matches when every cell of the key equals the stored value of its column. The key may hold any subset of the table's columns: just the identifying columns, or the whole row.
- A key with no cells matches every row, so the update applies to the whole table.
- Every matching row is updated. An update that matches no row is a no-op; neither case throws.
- Columns that are not in the set are left untouched. Key columns may be changed themselves.
- Updates run one after another in the supplied order, so a later update sees the effect of an earlier one, and when two updates change the same row the later one wins.
- An update with no cells to set is skipped (`SET` with nothing is invalid SQL). Nothing is executed when a table has no updates or only such updates.
- A column that appears twice in one key must equal both values, so two different values match nothing. A column that appears twice in one set is rejected by PostgreSQL (`multiple assignments to same column`).

A table's command runs in one implicit transaction: when any of its statements fails, none of them is applied and the `PostgresException` is thrown from the member that triggered the execution. This is how foreign keys and unique indexes (as created by `Pure.RelationalSchema.Storage.PostgreSQL.Creation`) are enforced: the statements are plain, and PostgreSQL's constraints decide the outcome. The package does not check them itself and does not cascade. A cell whose column does not belong to the table is rejected by PostgreSQL in the same way (`column … does not exist`). Commands of different tables are independent.

The result exposes **only the tables present in the supplied groupings**, not every table of the wrapped dataset. This matches `PostgreSqlStoredSchemaDataSetWithInsertedRows` from the Insertion package and `PostgreSqlStoredSchemaDataSetWithDeletedRows` from the Deletion package.

This package covers updates only. Creating the tables, reading rows, inserting rows and deleting rows are handled by the related packages listed below. The tables must already exist with the hash-based names produced by `Pure.RelationalSchema.Storage.PostgreSQL.Creation`. Schema, table and column identifiers are the first 63 hexadecimal characters of the hashes from `Pure.RelationalSchema.HashCodes`, not the model names. The caller owns the `IDbConnection`: it must be opened before use, and this package never disposes it.

## Public API

| Type | Kind | Description |
|------|------|-------------|
| `PostgreSqlStoredSchemaDataSetWithUpdatedRows` | record | Wraps an `IPostgreSqlStoredSchemaDataSet` and updates the rows of the corresponding tables; each update sets its cells on every row matching its key; implements `IPostgreSqlStoredSchemaDataSet` |

```csharp
public PostgreSqlStoredSchemaDataSetWithUpdatedRows(
    IPostgreSqlStoredSchemaDataSet dataset,
    IEnumerable<IGrouping<
        ITable,
        IGrouping<
            IEnumerable<KeyValuePair<IColumn, ICell>>,
            KeyValuePair<IColumn, ICell>>>> updates)
```

The constructor takes any `IPostgreSqlStoredSchemaDataSet`. The table-level decorator, `UpdateStatement` and the hashing, escaping and comparison helpers are internal. `IPostgreSqlStoredSchemaDataSet` comes from `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`; this package does not depend on Querying, Creation, Insertion or Deletion.

## Dependencies

- [`Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Abstractions/tree/0.1.0-preview.0.2.0) — provides `IPostgreSqlStoredSchemaDataSet`
- [`Pure.RelationalSchema.Storage.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.Abstractions/tree/0.1.0-preview.4.1.0) — storage abstractions (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`, `ICell`) that this package implements and consumes
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — hash codes used to derive PostgreSQL object names
- [`Pure.Primitives.Abstractions`](https://github.com/kudima03/Pure.Primitives.Abstractions/tree/4.3.0) — base interfaces for primitive types (`IString`, `IBool`, etc.)
- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.7.0) — concrete primitive type implementations
- [`Pure.Primitives.String.Operations`](https://github.com/kudima03/Pure.Primitives.String.Operations/tree/1.7.0) — string composition utilities used to build SQL statements
- [`Pure.Primitives.Choices`](https://github.com/kudima03/Pure.Primitives.Choices/tree/1.3.0) — `StringChoice`, used to leave out the `WHERE` of an empty key
- [`Pure.Linq.Conditions`](https://github.com/kudima03/Pure.Linq.Conditions/tree/0.1.0-preview.0.3.0) — `EmptyCondition`, which tells whether a key has cells
- [`Pure.Collections.Generic`](https://github.com/kudima03/Pure.Collections.Generic/tree/0.1.0-preview.3.0.0) — generic collection implementations (e.g. `OrderedDictionary` keyed by `TableHash`)

## Target Frameworks

- .NET 8
- .NET 9
- .NET 10
- .NET 11

## Installation

```
dotnet add package Pure.RelationalSchema.Storage.PostgreSQL.Updating
```

This package does not create tables, read rows or insert rows itself. The example below also uses `Pure.RelationalSchema.Storage.PostgreSQL.Creation` to create the tables, `Pure.RelationalSchema.Storage.PostgreSQL.Querying` for the wrapped dataset and `Pure.RelationalSchema.Storage.PostgreSQL.Insertion` to store the rows first; none of them is a dependency of this package.

## Usage

```csharp
using System.Collections;
using System.Data;
using Npgsql;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Creation;
using Pure.RelationalSchema.Storage.PostgreSQL.Insertion;
using Pure.RelationalSchema.Storage.PostgreSQL.Querying;
using Pure.RelationalSchema.Storage.PostgreSQL.Updating;
using Column = Pure.RelationalSchema.Column.Column;
using Schema = Pure.RelationalSchema.Schema.Schema;
using String = Pure.Primitives.String.String;
using Table = Pure.RelationalSchema.Table.Table;

IDbConnection connection = new NpgsqlConnection(connectionString);
connection.Open();

IColumn idColumn = new Column(new String("Id"), new IntColumnType());
IColumn nameColumn = new Column(new String("Name"), new StringColumnType());
ITable usersTable = new Table(new String("Users"), [idColumn, nameColumn], []);
ISchema schema = new Schema(new String("Shop"), [usersTable], []);

ISchema createdSchema = new PostgreSqlCreatedSchema(schema, connection);
_ = createdSchema.Tables.ToArray();

IPostgreSqlStoredSchemaDataSet dataSet =
    new PostgreSqlStoredSchemaDataSet(createdSchema, connection);

IRow alice = User("1", "Alice");
IRow bob = User("2", "Bob");

IPostgreSqlStoredSchemaDataSet withInserted =
    new PostgreSqlStoredSchemaDataSetWithInsertedRows(
        dataSet,
        [new TableRows(usersTable, [alice, bob])]
    );

// Both decorators are lazy: the INSERT runs on first access to the table dataset.
_ = withInserted[usersTable].TableSchema;

// Alice is matched by her id only; Bob by all of his cells. Carol is not stored,
// so the third update is a no-op.
IPostgreSqlStoredSchemaDataSet withUpdated =
    new PostgreSqlStoredSchemaDataSetWithUpdatedRows(
        dataSet,
        [
            new TableUpdates(
                usersTable,
                [
                    new Update([Cell(idColumn, "1")], [Cell(nameColumn, "Alicia")]),
                    new Update(bob.Cells, [Cell(nameColumn, "Robert O'Brien")]),
                    new Update([Cell(idColumn, "3")], [Cell(nameColumn, "Carol")]),
                ]
            ),
        ]
    );

foreach (IRow row in withUpdated[usersTable])
{
    Console.WriteLine(row.Cells[nameColumn].Value.TextValue); // Alicia, Robert O'Brien
}

KeyValuePair<IColumn, ICell> Cell(IColumn column, string value)
{
    return new KeyValuePair<IColumn, ICell>(column, new Cell(new String(value)));
}

IRow User(string id, string name)
{
    return new Row(
        new Dictionary<KeyValuePair<IColumn, ICell>, IColumn, ICell>(
            [Cell(idColumn, id), Cell(nameColumn, name)],
            cell => cell.Key,
            cell => cell.Value,
            column => new ColumnHash(column)
        )
    );
}

// One update: the key identifies the rows, the elements are the cells to set.
internal sealed class Update
    : IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>
{
    private readonly IEnumerable<KeyValuePair<IColumn, ICell>> _cells;

    public Update(
        IEnumerable<KeyValuePair<IColumn, ICell>> key,
        IEnumerable<KeyValuePair<IColumn, ICell>> cells
    )
    {
        Key = key;
        _cells = cells;
    }

    public IEnumerable<KeyValuePair<IColumn, ICell>> Key { get; }

    public IEnumerator<KeyValuePair<IColumn, ICell>> GetEnumerator()
    {
        return _cells.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

// The updates of one table, in the order they are executed.
internal sealed class TableUpdates
    : IGrouping<
        ITable,
        IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>
    >
{
    private readonly IEnumerable<
        IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>
    > _updates;

    public TableUpdates(
        ITable key,
        IEnumerable<
            IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>
        > updates
    )
    {
        Key = key;
        _updates = updates;
    }

    public ITable Key { get; }

    public IEnumerator<
        IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>
    > GetEnumerator()
    {
        return _updates.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

// Rows for the Insertion package, grouped by table.
internal sealed class TableRows : IGrouping<ITable, IRow>
{
    private readonly IEnumerable<IRow> _rows;

    public TableRows(ITable key, IEnumerable<IRow> rows)
    {
        Key = key;
        _rows = rows;
    }

    public ITable Key { get; }

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
```

`GroupBy` cannot be used to build the groupings: it hashes the key, and neither `ITable` implementations nor key enumerables support value-based `GetHashCode`, so supply your own `IGrouping` types as above. Every cell value is an `IString`, compared with or written to the stored value after PostgreSQL coerces the quoted literal to the column type.

## Known limitations

- **Values are inlined.** Values are written as quoted string literals with embedded single quotes doubled; backslashes are kept as they are (`standard_conforming_strings` is on by default). Values are not sent as parameters, so do not pass untrusted input.
- **One statement per update.** Each update is its own `UPDATE`; a table's statements travel in one command, so a very large batch produces very large SQL text.
- **No checks of its own.** A cell whose column is not part of the table, a column set twice and constraint violations are reported by PostgreSQL when the table's command runs, not when the decorator is built.
- **Supply each table once.** The groupings are keyed by `TableHash`; two groupings for the same table make the lookups (`this[]`, `ContainsKey`, `TryGetValue`, `Count`) throw. Put all updates of a table into one grouping, as with Insertion and Deletion.
- **Only supplied tables are exposed.** Tables of the wrapped dataset that have no grouping are not exposed by the result.

## Related packages

- `Pure.RelationalSchema.Storage.PostgreSQL.Creation` — creates the schema and tables this package updates
- `Pure.RelationalSchema.Storage.PostgreSQL.Querying` — reads rows from those tables; `PostgreSqlStoredSchemaDataSet` is the usual dataset to wrap
- `Pure.RelationalSchema.Storage.PostgreSQL.Insertion` — inserts rows into those tables
- `Pure.RelationalSchema.Storage.PostgreSQL.Deletion` — deletes rows from those tables
- `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` — provides `IPostgreSqlStoredSchemaDataSet`
