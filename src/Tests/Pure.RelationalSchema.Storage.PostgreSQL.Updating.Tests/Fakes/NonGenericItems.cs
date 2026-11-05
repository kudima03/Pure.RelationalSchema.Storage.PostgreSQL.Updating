using System.Collections;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating.Tests.Fakes;

internal sealed record NonGenericItems : IEnumerable<object>
{
    private readonly IEnumerable _source;

    public NonGenericItems(IEnumerable source)
    {
        _source = source;
    }

    public IEnumerator<object> GetEnumerator()
    {
        foreach (object item in _source)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
