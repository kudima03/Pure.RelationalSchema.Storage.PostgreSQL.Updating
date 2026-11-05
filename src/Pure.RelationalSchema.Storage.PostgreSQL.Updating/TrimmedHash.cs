using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Number;
using Pure.Primitives.String.Operations;
using Char = Pure.Primitives.Char.Char;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating;

internal sealed record TrimmedHash : IString
{
    private const ushort HashLength = 63;

    private readonly IString _sourceHash;

    public TrimmedHash(IString sourceHash)
    {
        _sourceHash = sourceHash;
    }

    public string TextValue =>
        new Substring(_sourceHash, new MinUshort(), new UShort(HashLength)).TextValue;

    public IEnumerator<IChar> GetEnumerator()
    {
        return TextValue.Select(x => new Char(x)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
