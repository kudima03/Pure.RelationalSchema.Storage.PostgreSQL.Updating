using System.Collections;
using System.Data;
using System.Linq.Expressions;
using Pure.Primitives.Abstractions.Bool;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Bool;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Updating;

internal sealed record PostgreSqlStoredTableDataSetWithUpdatedRows : IStoredTableDataSet
{
    private readonly IStoredTableDataSet _dataSet;

    private readonly Lazy<IBool> _rowsUpdated;

    public PostgreSqlStoredTableDataSetWithUpdatedRows(
        IStoredTableDataSet dataSet,
        IDbConnection connection,
        IString schemaName,
        IEnumerable<
            IGrouping<
                IEnumerable<KeyValuePair<IColumn, ICell>>,
                KeyValuePair<IColumn, ICell>
            >
        > updates
    )
    {
        _dataSet = dataSet;
        _rowsUpdated = new Lazy<IBool>(() =>
        {
            IEnumerable<IString> statements =
            [
                .. updates
                    .Where(update => update.Any())
                    .Select(update => new UpdateStatement(
                        dataSet.TableSchema,
                        schemaName,
                        update
                    )),
            ];

            if (!statements.Any())
            {
                return new True();
            }

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = new WhitespaceJoinedString(statements).TextValue;
            _ = command.ExecuteNonQuery();
            return new True();
        });
    }

    public ITable TableSchema =>
        _rowsUpdated.Value.BoolValue
            ? _dataSet.TableSchema
            : throw new ArgumentException("Value was not updated.");

    public Type ElementType =>
        _rowsUpdated.Value.BoolValue
            ? _dataSet.ElementType
            : throw new ArgumentException("Value was not updated.");

    public Expression Expression =>
        _rowsUpdated.Value.BoolValue
            ? _dataSet.Expression
            : throw new ArgumentException("Value was not updated.");

    public IQueryProvider Provider =>
        _rowsUpdated.Value.BoolValue
            ? _dataSet.Provider
            : throw new ArgumentException("Value was not updated.");

    public IEnumerator<IRow> GetEnumerator()
    {
        return _rowsUpdated.Value.BoolValue
            ? _dataSet.GetEnumerator()
            : throw new ArgumentException("Value was not updated.");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IAsyncEnumerator<IRow> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        return _rowsUpdated.Value.BoolValue
            ? _dataSet.GetAsyncEnumerator(cancellationToken)
            : throw new ArgumentException("Value was not updated.");
    }
}
