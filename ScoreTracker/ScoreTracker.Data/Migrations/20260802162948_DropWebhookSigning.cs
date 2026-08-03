using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Removes HMAC request signing. Authenticity is the maker's own header over TLS — one
    ///     <c>if</c> in their handler — and the signature was a crypto layer we owned for a benefit
    ///     nobody in this audience could use (owner, 2026-08-02).
    ///     <para>
    ///         <b>Hand-corrected from the scaffold.</b> EF matched the two <c>Tool</c> columns by
    ///         shape rather than by meaning and proposed dropping <c>OutboundHeaderValueHash</c>
    ///         while renaming <c>SigningSecretHash</c> into its place — which discards the value a
    ///         maker actually configured and promotes the dead signing secret in its stead. The end
    ///         schema is identical either way, so nothing downstream would have complained; the data
    ///         in it would have been wrong.
    ///     </para>
    ///     <para>
    ///         The rename of <c>OutboundHeaderValueHash</c> is the point of the exercise as much as
    ///         the drop is: the column has always held plaintext, because we send it verbatim on
    ///         every delivery. Now that it is the only thing authenticating us to a maker, a name
    ///         implying it is hashed is worse than cosmetic — the obvious "fix" for the mismatch
    ///         would be to start hashing it, which would silently break every delivery.
    ///     </para>
    /// </summary>
    public partial class DropWebhookSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SigningSecretHash",
                schema: "scores",
                table: "Tool");

            migrationBuilder.RenameColumn(
                name: "OutboundHeaderValueHash",
                schema: "scores",
                table: "Tool",
                newName: "OutboundHeaderValue");

            migrationBuilder.DropColumn(
                name: "Signature",
                schema: "scores",
                table: "WebhookDelivery");

            migrationBuilder.RenameColumn(
                name: "SignedAt",
                schema: "scores",
                table: "WebhookDelivery",
                newName: "QueuedAt");

            migrationBuilder.RenameIndex(
                name: "IX_WebhookDelivery_ToolId_SignedAt",
                schema: "scores",
                table: "WebhookDelivery",
                newName: "IX_WebhookDelivery_ToolId_QueuedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_WebhookDelivery_ToolId_QueuedAt",
                schema: "scores",
                table: "WebhookDelivery",
                newName: "IX_WebhookDelivery_ToolId_SignedAt");

            migrationBuilder.RenameColumn(
                name: "QueuedAt",
                schema: "scores",
                table: "WebhookDelivery",
                newName: "SignedAt");

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                schema: "scores",
                table: "WebhookDelivery",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "OutboundHeaderValue",
                schema: "scores",
                table: "Tool",
                newName: "OutboundHeaderValueHash");

            // Comes back empty. The secrets it held are gone and cannot be recovered — which is
            // fine, because nothing signs any more and a re-minted secret would be as good.
            migrationBuilder.AddColumn<string>(
                name: "SigningSecretHash",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
