using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class OriginalMix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalMixId",
                schema: "scores",
                table: "Chart",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalMixId",
                schema: "scores",
                table: "Chart");
        }
    }
}
