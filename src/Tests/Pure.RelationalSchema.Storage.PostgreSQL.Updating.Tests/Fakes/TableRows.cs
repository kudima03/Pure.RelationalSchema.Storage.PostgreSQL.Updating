using System.Collections;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record TableRows : IGrouping<ITable, IRow>
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
