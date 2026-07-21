using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByUserId",
                schema: "scores",
                table: "CommunityMembership",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "JoinedAt",
                schema: "scores",
                table: "CommunityMembership",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Permissions",
                schema: "scores",
                table: "CommunityMembership",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "scores",
                table: "CommunityMembership",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Member");

            // Seed = ManageInviteLinks | ManageUsers | ManageChannelSubscriptions (CommunityPermission).
            migrationBuilder.AddColumn<int>(
                name: "DefaultAdminPermissions",
                schema: "scores",
                table: "Community",
                type: "int",
                nullable: false,
                defaultValue: 13);

            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                schema: "scores",
                table: "Community",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMembership_CommunityId_Role",
                schema: "scores",
                table: "CommunityMembership",
                columns: new[] { "CommunityId", "Role" });

            // Backfill: the owner of a non-regional community becomes its Creator. Owners are not
            // members today, so promote an existing membership row where one exists, and synthesize
            // a Creator row where it doesn't. Regional/World communities are ownerless (Guid.Empty)
            // and stay all-Member.
            migrationBuilder.Sql(@"
                UPDATE cm SET Role = 'Creator'
                FROM [scores].[CommunityMembership] cm
                INNER JOIN [scores].[Community] c ON c.Id = cm.CommunityId
                WHERE c.IsRegional = 0 AND cm.UserId = c.OwningUserId;");

            migrationBuilder.Sql(@"
                INSERT INTO [scores].[CommunityMembership] (Id, CommunityId, UserId, Role, Permissions)
                SELECT NEWID(), c.Id, c.OwningUserId, 'Creator', 0
                FROM [scores].[Community] c
                WHERE c.IsRegional = 0
                  AND c.OwningUserId <> '00000000-0000-0000-0000-000000000000'
                  AND NOT EXISTS (
                    SELECT 1 FROM [scores].[CommunityMembership] m
                    WHERE m.CommunityId = c.Id AND m.UserId = c.OwningUserId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityMembership_CommunityId_Role",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.DropColumn(
                name: "GrantedByUserId",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.DropColumn(
                name: "Permissions",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "scores",
                table: "CommunityMembership");

            migrationBuilder.DropColumn(
                name: "DefaultAdminPermissions",
                schema: "scores",
                table: "Community");

            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                schema: "scores",
                table: "Community");
        }
    }
}
