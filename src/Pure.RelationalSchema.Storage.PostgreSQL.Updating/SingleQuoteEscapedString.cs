using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Char = Pure.Primitives.Char.Char;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating;

internal sealed record SingleQuoteEscapedString : IString
{
    private readonly IString _value;

    public SingleQuoteEscapedString(IString value)
    {
        _value = value;
    }

    public string TextValue =>
        new JoinedString(
            new ConcatenatedString(new SingleQuoteString(), new SingleQuoteString()),
            _value.TextValue.Split('\'').Select(x => new String(x))
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
