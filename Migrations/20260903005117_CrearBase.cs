using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class CrearBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdBase",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bases",
                columns: table => new
                {
                    IdBase = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Rinde = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bases", x => x.IdBase);
                    table.CheckConstraint("CK_Bases_Rinde", "\"Rinde\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdBase",
                table: "Productos",
                column: "IdBase");

            migrationBuilder.CreateIndex(
                name: "IX_Bases_Nombre",
                table: "Bases",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Bases_IdBase",
                table: "Productos",
                column: "IdBase",
                principalTable: "Bases",
                principalColumn: "IdBase",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Bases_IdBase",
                table: "Productos");

            migrationBuilder.DropTable(
                name: "Bases");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdBase",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdBase",
                table: "Productos");
        }
    }
}
