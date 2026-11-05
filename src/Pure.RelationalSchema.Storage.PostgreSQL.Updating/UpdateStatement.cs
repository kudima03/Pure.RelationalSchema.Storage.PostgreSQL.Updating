using System.Collections;
using Pure.Linq.Conditions;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Choices.String;
using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Char = Pure.Primitives.Char.Char;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating;

internal sealed record UpdateStatement : IString
{
    private readonly IString _tableName;

    private readonly IString _schemaName;

    private readonly IEnumerable<KeyValuePair<IColumn, ICell>> _key;

    private readonly IEnumerable<KeyValuePair<IColumn, ICell>> _cells;

    public UpdateStatement(
        ITable table,
        IString schemaName,
        IGrouping<
            IEnumerable<KeyValuePair<IColumn, ICell>>,
            KeyValuePair<IColumn, ICell>
        > update
    )
        : this(new HexString(new TableHash(table)), schemaName, update.Key, update) { }

    public UpdateStatement(
        IString tableName,
        IString schemaName,
        IEnumerable<KeyValuePair<IColumn, ICell>> key,
        IEnumerable<KeyValuePair<IColumn, ICell>> cells
    )
    {
        _tableName = tableName;
        _schemaName = schemaName;
        _key = key;
        _cells = cells;
    }

    public string TextValue =>
        new ConcatenatedString(
            new WhitespaceJoinedString(
                new String("UPDATE"),
                new JoinedString(
                    new DotString(),
                    [
                        new WrappedString(
                            new DoubleQuoteString(),
                            new TrimmedHash(_schemaName)
                        ),
                        new WrappedString(
                            new DoubleQuoteString(),
                            new TrimmedHash(_tableName)
                        ),
                    ]
                ),
                new String("SET"),
                new JoinedString(
                    new ConcatenatedString(new CommaString(), new WhitespaceString()),
                    _cells.Select(cell => new CellEquality(cell))
                )
            ),
            new StringChoice(
                new EmptyCondition<KeyValuePair<IColumn, ICell>>(_key),
                new EmptyString(),
                new ConcatenatedString(
                    new WhitespaceString(),
                    new WhitespaceJoinedString(
                        new String("WHERE"),
                        new JoinedString(
                            new WrappedString(new WhitespaceString(), new String("AND")),
                            _key.Select(cell => new CellEquality(cell))
                        )
                    )
                )
            ),
            new SemicolonString()
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
