using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreativeCollab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedDisplayName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill from the existing names. Accounts made before names were unique may share one: the later
            // ones get a short id suffix so the unique index can be built, and they can be renamed afterwards.
            migrationBuilder.Sql("UPDATE AspNetUsers SET NormalizedDisplayName = upper(DisplayName);");
            migrationBuilder.Sql(@"UPDATE AspNetUsers
                SET DisplayName = DisplayName || ' ' || substr(Id, 1, 8),
                    NormalizedDisplayName = NormalizedDisplayName || ' ' || upper(substr(Id, 1, 8))
                WHERE rowid NOT IN (SELECT min(rowid) FROM AspNetUsers GROUP BY NormalizedDisplayName);");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedDisplayName",
                table: "AspNetUsers",
                column: "NormalizedDisplayName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedDisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedDisplayName",
                table: "AspNetUsers");
        }
    }
}
