using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tool",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Visibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptsAllToolsShare = table.Column<bool>(type: "bit", nullable: false),
                    WebhookMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WebhookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SigningSecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OutboundHeaderName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OutboundHeaderValueHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tool", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolActivity",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolActivity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolApiKey",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Last4 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolApiKey", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolBlock",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolBlock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolInviteCode",
                schema: "scores",
                columns: table => new
                {
                    InviteCode = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolInviteCode", x => x.InviteCode);
                });

            migrationBuilder.CreateTable(
                name: "ToolMixSubscription",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolMixSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolShare",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolShare", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolSharePreference",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareWithAllTools = table.Column<bool>(type: "bit", nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolSharePreference", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDelivery",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeliveryId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RemoteStatusCode = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RemoteBodySnippet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsTest = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDelivery", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolActivity_ToolId_OccurredAt",
                schema: "scores",
                table: "ToolActivity",
                columns: new[] { "ToolId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolApiKey_KeyHash",
                schema: "scores",
                table: "ToolApiKey",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolBlock_ToolId_UserId",
                schema: "scores",
                table: "ToolBlock",
                columns: new[] { "ToolId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolMixSubscription_ToolId_MixId",
                schema: "scores",
                table: "ToolMixSubscription",
                columns: new[] { "ToolId", "MixId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolShare_ToolId_UserId",
                schema: "scores",
                table: "ToolShare",
                columns: new[] { "ToolId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolShare_UserId",
                schema: "scores",
                table: "ToolShare",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_NextAttemptAt",
                schema: "scores",
                table: "WebhookDelivery",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_ToolId_SignedAt",
                schema: "scores",
                table: "WebhookDelivery",
                columns: new[] { "ToolId", "SignedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tool",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolActivity",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolApiKey",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolBlock",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolInviteCode",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolMixSubscription",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolShare",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ToolSharePreference",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "WebhookDelivery",
                schema: "scores");
        }
    }
}
