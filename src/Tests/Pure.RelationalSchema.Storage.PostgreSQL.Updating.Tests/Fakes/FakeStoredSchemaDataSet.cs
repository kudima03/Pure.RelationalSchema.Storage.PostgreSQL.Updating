using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Pure.Collections.Generic;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record FakeStoredSchemaDataSet : IPostgreSqlStoredSchemaDataSet
{
    private readonly IReadOnlyDictionary<ITable, IStoredTableDataSet> _tablesDatasets;

    public FakeStoredSchemaDataSet(
        ISchema schema,
        IDbConnection connection,
        Func<ITable, IEnumerable<IRow>> rowsOfTable
    )
    {
        Schema = schema;
        Connection = connection;
        _tablesDatasets = new Dictionary<ITable, ITable, IStoredTableDataSet>(
            schema.Tables,
            table => table,
            table => new FakeStoredTableDataSet(table, rowsOfTable(table)),
            table => new TableHash(table)
        );
    }

    public ISchema Schema { get; }

    public IDbConnection Connection { get; }

    public IEnumerable<ITable> Keys => _tablesDatasets.Keys;

    public IEnumerable<IStoredTableDataSet> Values => _tablesDatasets.Values;

    public int Count => _tablesDatasets.Count;

    public IStoredTableDataSet this[ITable key] => _tablesDatasets[key];

    public bool ContainsKey(ITable key)
    {
        return _tablesDatasets.ContainsKey(key);
    }

    public bool TryGetValue(
        ITable key,
        [MaybeNullWhen(false)] out IStoredTableDataSet value
    )
    {
        return _tablesDatasets.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<ITable, IStoredTableDataSet>> GetEnumerator()
    {
        return _tablesDatasets.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
