using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.DevTooling;

/// <summary>
///     Writes a downloaded catalog into the local database.
///     <para>
///         Every column is assigned by name here rather than copied across from a matching remote
///         row. That is the point: the mapping from the public API's shape onto local columns is a
///         thing someone can read, and adding a NOT NULL column breaks the build instead of
///         producing a local database that is subtly wrong.
///     </para>
///     <para>
///         Bulk copy under one transaction, because a half-written catalog is a database whose
///         chart ids do not resolve — worse than an empty one.
///     </para>
/// </summary>
internal sealed class DevCatalogWriter : IDevCatalogWriter
{
    private const string Schema = "scores";

    /// <summary>
    ///     Reverse FK order. Scores and saved charts point at Chart, so they clear before the
    ///     catalog they depend on does.
    /// </summary>
    private static readonly string[] ClearOrder =
    {
        "PhoenixRecord", "SavedChart", "ChartScoringLevel", "TierListEntry", "ChartMix", "Chart", "Song", "Mix"
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public DevCatalogWriter(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task ReplaceCatalog(DevCatalogSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var table in ClearOrder)
            await Execute(connection, transaction, $"DELETE FROM [{Schema}].[{table}]", cancellationToken);

        // Song ids are local surrogate keys — the wire keys songs by name, which is what the rest of
        // the catalog references them by too. Minted here and used for the Chart rows below.
        var songIds = snapshot.Songs.ToDictionary(s => s.Name, _ => Guid.NewGuid(), StringComparer.Ordinal);

        await Insert(connection, transaction, "Mix", snapshot.Mixes, (row, m) =>
        {
            row["Id"] = MixIds.For(m.Mix);
            row["Name"] = m.DisplayName;
            row["SortOrder"] = m.SortOrder;
            row["IsPrimary"] = m.IsPrimary;
        }, cancellationToken);

        await Insert(connection, transaction, "Song", snapshot.Songs, (row, s) =>
        {
            row["Id"] = songIds[s.Name];
            row["Name"] = s.Name;
            row["Type"] = s.Type;
            row["Artist"] = s.Artist;
            // Stored as ticks (HasConversion<long> on SongEntity.Duration), so the DataTable column
            // is bigint and a TimeSpan will not go in it.
            row["Duration"] = TimeSpan.FromSeconds(s.DurationSeconds).Ticks;
            row["ImagePath"] = s.ImageUrl;
            row["MinBpm"] = (object?)s.MinBpm ?? DBNull.Value;
            row["MaxBpm"] = (object?)s.MaxBpm ?? DBNull.Value;
        }, cancellationToken);

        // A chart id repeats across the mixes it exists in. Chart is one row per id; ChartMix is one
        // per (chart, mix) and carries that mix's level and note count.
        var charts = snapshot.Charts
            .GroupBy(c => c.ChartId)
            .Select(g => g.OrderBy(c => c.Mix == c.OriginalMix ? 0 : 1).First())
            .Where(c => songIds.ContainsKey(c.SongName))
            .ToArray();

        await Insert(connection, transaction, "Chart", charts, (row, c) =>
        {
            row["Id"] = c.ChartId;
            row["SongId"] = songIds[c.SongName];
            row["Level"] = c.Level;
            row["Type"] = c.Type;
            row["StepArtist"] = (object?)c.StepArtist ?? DBNull.Value;
            row["OriginalMixId"] = MixIds.For(c.OriginalMix);
            row["PlayerCount"] = c.PlayerCount;
        }, cancellationToken);

        var known = charts.Select(c => c.ChartId).ToHashSet();
        await Insert(connection, transaction, "ChartMix",
            snapshot.Charts.Where(c => known.Contains(c.ChartId)), (row, c) =>
            {
                row["Id"] = Guid.NewGuid();
                row["ChartId"] = c.ChartId;
                row["MixId"] = MixIds.For(c.Mix);
                row["Level"] = c.Level;
                row["NoteCount"] = (object?)c.NoteCount ?? DBNull.Value;
                row["LegacySlot"] = (object?)c.LegacySlot ?? DBNull.Value;
            }, cancellationToken);

        await Insert(connection, transaction, "TierListEntry",
            snapshot.TierListEntries.Where(t => known.Contains(t.ChartId)), (row, t) =>
            {
                row["Id"] = Guid.NewGuid();
                row["TierListName"] = t.ListName;
                row["ChartId"] = t.ChartId;
                row["MixId"] = MixIds.For(t.Mix);
                row["Category"] = t.Category;
                row["Order"] = t.Order;
            }, cancellationToken);

        await Insert(connection, transaction, "ChartScoringLevel",
            snapshot.ScoringLevels.Where(s => known.Contains(s.ChartId)), (row, s) =>
            {
                row["Id"] = Guid.NewGuid();
                row["ChartId"] = s.ChartId;
                row["MixId"] = MixIds.For(s.Mix);
                row["ScoringLevel"] = s.ScoringLevel;
            }, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceUserScores(Guid localUserId, IReadOnlyList<DevScoreRow> scores,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await Execute(connection, transaction,
            $"DELETE FROM [{Schema}].[PhoenixRecord] WHERE UserId = @userId", cancellationToken, localUserId);

        // A score whose chart is not in the local catalog would be invisible and would break every
        // join that assumes otherwise. Dropping it is the honest outcome of a partial catalog.
        var chartIds = await ReadChartIds(connection, transaction, cancellationToken);

        await Insert(connection, transaction, "PhoenixRecord",
            scores.Where(s => chartIds.Contains(s.ChartId)), (row, s) =>
            {
                row["Id"] = Guid.NewGuid();
                row["UserId"] = localUserId;
                row["ChartId"] = s.ChartId;
                row["MixId"] = MixIds.For(s.Mix);
                row["RecordedDate"] = s.RecordedAt;
                row["Score"] = (object?)s.Score ?? DBNull.Value;
                row["LetterGrade"] = (object?)s.LetterGrade ?? DBNull.Value;
                row["Plate"] = (object?)s.Plate ?? DBNull.Value;
                row["IsBroken"] = s.IsBroken;
                row["Source"] = (object?)s.Source ?? DBNull.Value;
                row["Perfects"] = (object?)s.Perfects ?? DBNull.Value;
                row["Greats"] = (object?)s.Greats ?? DBNull.Value;
                row["Goods"] = (object?)s.Goods ?? DBNull.Value;
                row["Bads"] = (object?)s.Bads ?? DBNull.Value;
                row["Misses"] = (object?)s.Misses ?? DBNull.Value;
                row["MaxCombo"] = (object?)s.MaxCombo ?? DBNull.Value;
            }, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<HashSet<Guid>> ReadChartIds(SqlConnection connection, SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        await using var command = new SqlCommand($"SELECT Id FROM [{Schema}].[Chart]", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));

        return ids;
    }

    private static async Task Execute(SqlConnection connection, SqlTransaction transaction, string sql,
        CancellationToken cancellationToken, Guid? userId = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        if (userId != null) command.Parameters.AddWithValue("@userId", userId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    ///     The local schema shapes the DataTable, so a column this mapping does not set arrives as
    ///     the database's own default rather than as a guess.
    /// </summary>
    private static async Task Insert<T>(SqlConnection connection, SqlTransaction transaction, string table,
        IEnumerable<T> rows, Action<DataRow, T> map, CancellationToken cancellationToken)
    {
        var schemaTable = new DataTable();
        await using (var schemaCommand =
                     new SqlCommand($"SELECT TOP 0 * FROM [{Schema}].[{table}]", connection, transaction))
        await using (var schemaReader = await schemaCommand.ExecuteReaderAsync(cancellationToken))
        {
            schemaTable.Load(schemaReader);
        }

        var count = 0;
        foreach (var item in rows)
        {
            var dataRow = schemaTable.NewRow();
            foreach (DataColumn column in schemaTable.Columns) dataRow[column] = DBNull.Value;
            map(dataRow, item);
            schemaTable.Rows.Add(dataRow);
            count++;
        }

        if (count == 0) return;

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
}
