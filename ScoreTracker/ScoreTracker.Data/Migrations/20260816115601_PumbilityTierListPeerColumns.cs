using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PumbilityTierListPeerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CohortSize",
                schema: "scores",
                table: "PumbilityTierListEntry",
                newName: "PeerCount");

            migrationBuilder.RenameColumn(
                name: "CohortKey",
                schema: "scores",
                table: "PumbilityTierListEntry",
                newName: "PeerKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PeerCount",
                schema: "scores",
                table: "PumbilityTierListEntry",
                newName: "CohortSize");

            migrationBuilder.RenameColumn(
                name: "PeerKey",
                schema: "scores",
                table: "PumbilityTierListEntry",
                newName: "CohortKey");
        }
    }
}
