using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record Update
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
