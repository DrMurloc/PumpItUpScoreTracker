using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     A maker barred from making tools, which is rule 2's sanction and had no implementation:
    ///     deleting a tool never stopped its maker registering another thirty seconds later.
    ///     <para>
    ///         Every effect is computed from this row at read time rather than written into the
    ///         tools — their shares, keys, listings, activity log and delivery history are left
    ///         exactly as they were. That is what makes a ban liftable, and it keeps the evidence a
    ///         disputed ban would be argued over.
    ///     </para>
    /// </summary>
    public partial class ToolMakerBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolMakerBan",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BannedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolMakerBan", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolMakerBan",
                schema: "scores");
        }
    }
}
