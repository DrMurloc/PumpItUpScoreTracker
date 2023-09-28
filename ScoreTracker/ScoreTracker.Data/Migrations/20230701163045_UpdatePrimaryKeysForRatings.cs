using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class UpdatePrimaryKeysForRatings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChartDifficultyRating",
                schema: "scores",
                table: "ChartDifficultyRating");

            migrationBuilder.DropIndex(
                name: "IX_Chart_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.AlterColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "ChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChartDifficultyRating",
                schema: "scores",
                table: "ChartDifficultyRating",
                columns: new[] { "ChartId", "MixId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chart_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                columns: new[] { "DifficultyRatingChartId", "DifficultyRatingMixId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                columns: new[] { "DifficultyRatingChartId", "DifficultyRatingMixId" },
                principalSchema: "scores",
                principalTable: "ChartDifficultyRating",
                principalColumns: new[] { "ChartId", "MixId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChartDifficultyRating",
                schema: "scores",
                table: "ChartDifficultyRating");

            migrationBuilder.DropIndex(
                name: "IX_Chart_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropColumn(
                name: "DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.AlterColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "ChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChartDifficultyRating",
                schema: "scores",
                table: "ChartDifficultyRating",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_Chart_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                column: "DifficultyRatingChartId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                column: "DifficultyRatingChartId",
                principalSchema: "scores",
                principalTable: "ChartDifficultyRating",
                principalColumn: "ChartId");
        }
    }
}
