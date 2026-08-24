using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TranslationPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TranslationQueuedAt",
                schema: "scores",
                table: "ChartComment",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChartCommentRendering",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TranslatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartCommentRendering", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationBatch",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderBatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheCreationInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationRequest",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PivotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartCommentRendering_CommentId_Locale",
                schema: "scores",
                table: "ChartCommentRendering",
                columns: new[] { "CommentId", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationBatch_CompletedAt",
                schema: "scores",
                table: "TranslationBatch",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationRequest_BatchId",
                schema: "scores",
                table: "TranslationRequest",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationRequest_SourceKey",
                schema: "scores",
                table: "TranslationRequest",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslationRequest_State_CreatedAt",
                schema: "scores",
                table: "TranslationRequest",
                columns: new[] { "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartCommentRendering",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "TranslationBatch",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "TranslationRequest",
                schema: "scores");

            migrationBuilder.DropColumn(
                name: "TranslationQueuedAt",
                schema: "scores",
                table: "ChartComment");
        }
    }
}
