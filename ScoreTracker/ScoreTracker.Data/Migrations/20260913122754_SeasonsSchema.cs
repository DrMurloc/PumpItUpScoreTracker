using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeasonsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "SeasonId",
                schema: "scores",
                table: "PlayerStats",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<double>(
                name: "TotalPumbility",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<short>(
                name: "SeasonId",
                schema: "scores",
                table: "PlayerFolderLevel",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SeasonId",
                schema: "scores",
                table: "PhoenixRecord",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SeasonId",
                schema: "scores",
                table: "HardmodeChart",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            // Hand-ordered: each primary key drops right before it returns with the season in front,
            // so no table is without one for more than a statement while the previous app version
            // is still writing (docs/design/seasons.md §6.2).
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats",
                columns: new[] { "SeasonId", "UserId", "MixId" });

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerFolderLevel",
                schema: "scores",
                table: "PlayerFolderLevel");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerFolderLevel",
                schema: "scores",
                table: "PlayerFolderLevel",
                columns: new[] { "SeasonId", "UserId", "MixId", "ChartType", "Level" });

            migrationBuilder.DropPrimaryKey(
                name: "PK_HardmodeChart",
                schema: "scores",
                table: "HardmodeChart");

            migrationBuilder.AddPrimaryKey(
                name: "PK_HardmodeChart",
                schema: "scores",
                table: "HardmodeChart",
                columns: new[] { "SeasonId", "MixId", "ChartId" });

            migrationBuilder.CreateTable(
                name: "ChartSeason",
                schema: "scores",
                columns: table => new
                {
                    SeasonId = table.Column<short>(type: "smallint", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    PrintedLevel = table.Column<int>(type: "int", nullable: false),
                    MovedThisRoll = table.Column<short>(type: "smallint", nullable: false),
                    HoldWeight = table.Column<double>(type: "float", nullable: true),
                    HoldRank = table.Column<int>(type: "int", nullable: true),
                    LivePlayers = table.Column<int>(type: "int", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartSeason", x => new { x.SeasonId, x.MixId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_ChartSeason_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Season",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SealedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Season", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_SeasonId_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "SeasonId", "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "UserId", "Score", "Plate", "IsBroken" })
                .Annotation("SqlServer:Online", true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId_SeasonId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "UserId", "SeasonId", "ChartId", "MixId" },
                unique: true)
                .Annotation("SqlServer:Online", true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartSeason_ChartId",
                schema: "scores",
                table: "ChartSeason",
                column: "ChartId");

            // Hand-ordered: the season-first indexes above are built, online, before the old ones
            // go, so one row per player and chart stays enforced throughout the deploy window
            // (docs/design/seasons.md §6.2).
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A schema rollback for a database without season rows. Once slice 1b has written any, the
            // unique index and the primary keys below collide on them — delete the season rows first,
            // by hand and on purpose; nothing here deletes data.
            migrationBuilder.DropTable(
                name: "ChartSeason",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "Season",
                schema: "scores");

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "UserId", "Score", "Plate", "IsBroken" })
                .Annotation("SqlServer:Online", true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "UserId", "ChartId", "MixId" },
                unique: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerFolderLevel",
                schema: "scores",
                table: "PlayerFolderLevel");

            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_SeasonId_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId_SeasonId_ChartId_MixId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropPrimaryKey(
                name: "PK_HardmodeChart",
                schema: "scores",
                table: "HardmodeChart");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "TotalPumbility",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                schema: "scores",
                table: "PlayerFolderLevel");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                schema: "scores",
                table: "HardmodeChart");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerStats",
                schema: "scores",
                table: "PlayerStats",
                columns: new[] { "UserId", "MixId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerFolderLevel",
                schema: "scores",
                table: "PlayerFolderLevel",
                columns: new[] { "UserId", "MixId", "ChartType", "Level" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_HardmodeChart",
                schema: "scores",
                table: "HardmodeChart",
                columns: new[] { "MixId", "ChartId" });
        }
    }
}
