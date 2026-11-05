using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record FakeStoredTableDataSet : IStoredTableDataSet
{
    private readonly IQueryable<IRow> _rows;

    public FakeStoredTableDataSet(ITable tableSchema, IEnumerable<IRow> rows)
    {
        TableSchema = tableSchema;
        _rows = rows.AsQueryable();
    }

    public ITable TableSchema { get; }

    public Type ElementType => _rows.ElementType;

    public Expression Expression => _rows.Expression;

    public IQueryProvider Provider => _rows.Provider;

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public async IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        foreach (IRow row in _rows)
        {
            await Task.Delay(1, cancellationToken);

            yield return row;
        }
    }
}
