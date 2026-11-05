using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record TableUpdates
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
        params IEnumerable<
            IGrouping<
                IEnumerable<KeyValuePair<IColumn, ICell>>,
                KeyValuePair<IColumn, ICell>
            >
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
