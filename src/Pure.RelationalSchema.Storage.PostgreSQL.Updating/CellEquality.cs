using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Char = Pure.Primitives.Char.Char;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating;

internal sealed record CellEquality : IString
{
    private readonly KeyValuePair<IColumn, ICell> _cell;

    public CellEquality(KeyValuePair<IColumn, ICell> cell)
    {
        _cell = cell;
    }

    public string TextValue =>
        new WhitespaceJoinedString(
            new WrappedString(
                new DoubleQuoteString(),
                new TrimmedHash(new HexString(new ColumnHash(_cell.Key)))
            ),
            new EqualsSignString(),
            new WrappedString(
                new SingleQuoteString(),
                new SingleQuoteEscapedString(_cell.Value.Value)
            )
        ).TextValue;

    public IEnumerator<IChar> GetEnumerator()
    {
        return TextValue.Select(x => new Char(x)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
