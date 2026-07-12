using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserTierListEntryFreshness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTierListEntry_MixId_ChartId",
                schema: "scores",
                table: "UserTierListEntry");

            migrationBuilder.AddColumn<double>(
                name: "Freshness",
                schema: "scores",
                table: "UserTierListEntry",
                type: "float",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.CreateIndex(
                name: "IX_UserTierListEntry_MixId_ChartId",
                schema: "scores",
                table: "UserTierListEntry",
                columns: new[] { "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "Category", "Order", "Freshness" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTierListEntry_MixId_ChartId",
                schema: "scores",
                table: "UserTierListEntry");

            migrationBuilder.DropColumn(
                name: "Freshness",
                schema: "scores",
                table: "UserTierListEntry");

            migrationBuilder.CreateIndex(
                name: "IX_UserTierListEntry_MixId_ChartId",
                schema: "scores",
                table: "UserTierListEntry",
                columns: new[] { "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "Category", "Order" });
        }
    }
}
