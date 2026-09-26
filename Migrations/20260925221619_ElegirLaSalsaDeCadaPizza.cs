using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class ElegirLaSalsaDeCadaPizza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdSalsa",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdSalsa",
                table: "Productos",
                column: "IdSalsa");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Recetas_IdSalsa",
                table: "Productos",
                column: "IdSalsa",
                principalTable: "Recetas",
                principalColumn: "IdReceta",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Recetas_IdSalsa",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdSalsa",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdSalsa",
                table: "Productos");
        }
    }
}
