using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.DevTooling;

/// <summary>
///     Raw allowlisted table transfer for the dev harness. Reads and writes through plain
///     ADO on the context's connection — deliberately schema-shaped and unstable (see
///     <see cref="IDevDataTransfer" />). The allowlist maps logical keys to physical
///     tables; import order is FK-safe and deletes run in reverse before inserting.
/// </summary>
public sealed class DevDataTransfer : IDevDataTransfer
{
    // Ordered FK-safe: parents before children.
    private static readonly (string Key, string Table)[] ReferenceTables =
    {
        ("mixes", "Mix"),
        ("songs", "Song"),
        ("charts", "Chart"),
        ("chartmixes", "ChartMix"),
        ("tierlists", "TierListEntry"),
        ("scoringlevels", "ChartScoringLevel")
    };

    private const string ScoresTable = "PhoenixRecord";
    private const string Schema = "scores";

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public DevDataTransfer(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<string> ReferenceTableKeys { get; } = ReferenceTables.Select(t => t.Key).ToArray();

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExportReferenceRows(string tableKey,
        CancellationToken cancellationToken = default)
    {
        var table = ReferenceTables.FirstOrDefault(t => t.Key == tableKey).Table
                    ?? throw new ArgumentException($"Unknown table key '{tableKey}'", nameof(tableKey));
        return await ReadRows($"SELECT * FROM [{Schema}].[{table}]", null, cancellationToken);
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExportUserScores(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await ReadRows($"SELECT * FROM [{Schema}].[{ScoresTable}] WHERE UserId = @userId", userId,
            cancellationToken);
    }

    public async Task ReplaceReferenceTables(
        IReadOnlyDictionary<string, IReadOnlyList<Dictionary<string, JsonElement>>> rowsByTable,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        // Delete children first so FKs never block; PhoenixRecord/SavedChart reference
        // Chart, so clear them ahead of the catalog tables they point at.
        foreach (var dependent in new[] { ScoresTable, "SavedChart" })
            await ExecuteAsync(connection, transaction, $"DELETE FROM [{Schema}].[{dependent}]", cancellationToken);
        foreach (var (_, table) in ReferenceTables.Reverse())
            await ExecuteAsync(connection, transaction, $"DELETE FROM [{Schema}].[{table}]", cancellationToken);

        foreach (var (key, table) in ReferenceTables)
        {
            if (!rowsByTable.TryGetValue(key, out var rows) || rows.Count == 0) continue;

            await BulkInsert(connection, transaction, table, rows, null, null, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceUserScores(Guid localUserId,
        IReadOnlyList<Dictionary<string, JsonElement>> rows,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await ExecuteAsync(connection, transaction,
            $"DELETE FROM [{Schema}].[{ScoresTable}] WHERE UserId = @userId", cancellationToken, localUserId);
        if (rows.Count > 0)
            await BulkInsert(connection, transaction, ScoresTable, rows, "UserId", localUserId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRows(string sql, Guid? userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (userId != null) command.Parameters.AddWithValue("@userId", userId.Value);

        var results = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            results.Add(row);
        }

        return results;
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql,
        CancellationToken cancellationToken, Guid? userId = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        if (userId != null) command.Parameters.AddWithValue("@userId", userId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BulkInsert(SqlConnection connection, SqlTransaction transaction, string table,
        IReadOnlyList<Dictionary<string, JsonElement>> rows, string? overrideColumn, Guid? overrideValue,
        CancellationToken cancellationToken)
    {
        // Local schema drives typing: build a DataTable from SELECT TOP 0 and convert the
        // incoming JSON values per column type. Columns the local schema doesn't have are
        // ignored; columns the export didn't include insert as NULL/default.
        var schemaTable = new DataTable();
        await using (var schemaCommand =
                     new SqlCommand($"SELECT TOP 0 * FROM [{Schema}].[{table}]", connection, transaction))
        await using (var schemaReader = await schemaCommand.ExecuteReaderAsync(cancellationToken))
        {
            schemaTable.Load(schemaReader);
        }

        foreach (var row in rows)
        {
            var dataRow = schemaTable.NewRow();
            foreach (DataColumn column in schemaTable.Columns)
            {
                if (overrideColumn != null &&
                    string.Equals(column.ColumnName, overrideColumn, StringComparison.OrdinalIgnoreCase))
                {
                    dataRow[column] = overrideValue!;
                    continue;
                }

                if (!row.TryGetValue(column.ColumnName, out var value) ||
                    value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    dataRow[column] = DBNull.Value;
                    continue;
                }

                dataRow[column] = ConvertValue(value, column.DataType);
            }

            schemaTable.Rows.Add(dataRow);
        }

        using var bulkCopy = new SqlBulkCopy(connection,
            SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls, transaction)
        {
            DestinationTableName = $"[{Schema}].[{table}]",
            BulkCopyTimeout = 300
        };
        foreach (DataColumn column in schemaTable.Columns)
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulkCopy.WriteToServerAsync(schemaTable, cancellationToken);
    }

    private static object ConvertValue(JsonElement value, Type targetType)
    {
        if (targetType == typeof(Guid)) return value.GetGuid();
        if (targetType == typeof(string)) return value.ToString();
        if (targetType == typeof(bool)) return value.GetBoolean();
        if (targetType == typeof(int)) return value.GetInt32();
        if (targetType == typeof(long)) return value.GetInt64();
        if (targetType == typeof(short)) return value.GetInt16();
        if (targetType == typeof(byte)) return value.GetByte();
        if (targetType == typeof(double)) return value.GetDouble();
        if (targetType == typeof(float)) return value.GetSingle();
        if (targetType == typeof(decimal)) return value.GetDecimal();
        if (targetType == typeof(DateTimeOffset)) return value.GetDateTimeOffset();
        if (targetType == typeof(DateTime)) return value.GetDateTime();
        if (targetType == typeof(TimeSpan)) return TimeSpan.Parse(value.GetString()!);
        return value.ToString();
    }
}
