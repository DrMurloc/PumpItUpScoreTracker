using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Every stored PUMBILITY pool widens from int to float. A pool is fifty per-chart values
    ///     that each carry a real fraction, so an integer column spends precision that only the
    ///     presentation layer is entitled to spend.
    ///     <para>
    ///         Up is a widening and runs in place: no row is rewritten, no backfill is needed, and
    ///         existing values simply read back with a zero fraction until the next ratings sweep.
    ///         The scaffolder's data-loss warning is about Down, which truncates back to int and
    ///         would discard exactly the decimals this migration exists to keep.
    ///     </para>
    /// </summary>
    public partial class PumbilityPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "PumbilityGain",
                schema: "scores",
                table: "ScoreHighlight",
                type: "float",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "SkillRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "SinglesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "DoublesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "CoOpRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "SkillRating",
                schema: "scores",
                table: "PlayerHistory",
                type: "float",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "CoOpRating",
                schema: "scores",
                table: "PlayerHistory",
                type: "float",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PumbilityGain",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SkillRating",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "SinglesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "DoublesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "CoOpRating",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<int>(
                name: "SkillRating",
                schema: "scores",
                table: "PlayerHistory",
                type: "int",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CoOpRating",
                schema: "scores",
                table: "PlayerHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");
        }
    }
}
