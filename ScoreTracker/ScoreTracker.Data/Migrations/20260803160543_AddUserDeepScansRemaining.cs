using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeepScansRemaining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing accounts arrive with a full allowance rather than none: the reset only runs
            // on the 1st, so a zero default would silently deny every player their scans for up to
            // a month after deploy. 3 matches DeepScanAllowanceHandlers.MonthlyAllowance.
            migrationBuilder.AddColumn<int>(
                name: "DeepScansRemaining",
                schema: "scores",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 3); // DeepScanAllowance.PerMonth
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeepScansRemaining",
                schema: "scores",
                table: "User");
        }
    }
}
