using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     The rename table stops being a proposal queue and becomes the record of every tag
    ///     that left the boards, carrying the evidence behind each verdict.
    ///     <para>
    ///         Top50Overlap becomes BoardsPresent rather than being dropped: it counted the boards
    ///         the new tag stood on with a score at least the old one, which is what BoardsPresent
    ///         counts now. Scaffolding guessed it was SuspiciousAbsences, which would have started
    ///         every historical row looking like a ban.
    ///     </para>
    /// </summary>
    public partial class VanishedTagFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Top50Overlap",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                newName: "BoardsPresent");

            // A tag that left with nothing to point at has no candidate to record.
            migrationBuilder.AlterColumn<string>(
                name: "NewUsername",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "NewPlayerId",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "SuspiciousAbsences",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExactNonPgMatches",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExactPerfectGames",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OldPlacements",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunnerUpExactMatches",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every row predating this migration came from a detector that only ever proposed:
            // it recorded no verdict because there was only one to record. Saying so beats
            // leaving an empty string the desk would render as a blank heading.
            migrationBuilder.Sql(
                "UPDATE scores.OfficialPlayerRenameProposal SET Verdict = 'Propose' WHERE Verdict = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A tag recorded with no candidate cannot be expressed in the old shape, so it goes
            // rather than coming back as a proposal pointing at player zero.
            migrationBuilder.Sql(
                "DELETE FROM scores.OfficialPlayerRenameProposal WHERE NewPlayerId IS NULL");

            migrationBuilder.DropColumn(
                name: "SuspiciousAbsences",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.DropColumn(
                name: "ExactNonPgMatches",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.DropColumn(
                name: "ExactPerfectGames",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.DropColumn(
                name: "OldPlacements",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.DropColumn(
                name: "RunnerUpExactMatches",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.DropColumn(
                name: "Verdict",
                schema: "scores",
                table: "OfficialPlayerRenameProposal");

            migrationBuilder.RenameColumn(
                name: "BoardsPresent",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                newName: "Top50Overlap");

            migrationBuilder.AlterColumn<string>(
                name: "NewUsername",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NewPlayerId",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
