using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     The two webhook secrets, stored the two different ways their directions demand.
    ///     <para>
    ///         <c>OutboundHeaderValue</c> widens because it now holds an AES-GCM envelope rather than
    ///         the header text: we send it verbatim on every delivery, so it has to stay readable and
    ///         therefore encrypted rather than hashed.
    ///     </para>
    ///     <para>
    ///         <c>WebhookVerificationSecretHash</c> is the opposite. It is what a maker's endpoint
    ///         answers a verification request with, it never travels to that endpoint, and we only
    ///         ever compare — so a hash is both sufficient and safer than a recoverable copy.
    ///         Keeping them apart is the whole point: the header is a value anyone who receives one
    ///         delivery has already read, so reusing it as the proof would prove nothing.
    ///     </para>
    /// </summary>
    public partial class WebhookVerificationSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OutboundHeaderValue",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookVerificationSecretHash",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            // Any value written before this migration is plaintext, and the reader will now try to
            // open it as an envelope and get nothing. Clearing it makes the console say "not set",
            // which is true, instead of showing a header whose value silently never arrives. The
            // name is left alone so the maker can see what they had. Zero rows in production —
            // this feature ships with the same release — so it only matters to a dev database.
            migrationBuilder.Sql(
                "UPDATE scores.Tool SET OutboundHeaderValue = NULL WHERE OutboundHeaderValue IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookVerificationSecretHash",
                schema: "scores",
                table: "Tool");

            migrationBuilder.AlterColumn<string>(
                name: "OutboundHeaderValue",
                schema: "scores",
                table: "Tool",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);
        }
    }
}
