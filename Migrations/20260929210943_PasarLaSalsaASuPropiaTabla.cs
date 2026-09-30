using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class PasarLaSalsaASuPropiaTabla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductoSalsas",
                columns: table => new
                {
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    IdReceta = table.Column<int>(type: "integer", nullable: false),
                    Posicion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoSalsas", x => new { x.IdProducto, x.IdReceta });
                    table.ForeignKey(
                        name: "FK_ProductoSalsas_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoSalsas_Recetas_IdReceta",
                        column: x => x.IdReceta,
                        principalTable: "Recetas",
                        principalColumn: "IdReceta",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoSalsas_IdReceta",
                table: "ProductoSalsas",
                column: "IdReceta");

            // La salsa de cada producto pasa a la tabla nueva antes de que se
            // borre la columna. Con el lugar en cero, antes que los ingredientes,
            // que arrancan en 1: la carta la sigue nombrando primera.
            migrationBuilder.Sql(@"
                INSERT INTO ""ProductoSalsas"" (""IdProducto"", ""IdReceta"", ""Posicion"")
                SELECT ""IdProducto"", ""IdSalsa"", 0
                FROM ""Productos""
                WHERE ""IdSalsa"" IS NOT NULL;");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            // De vuelta a una sola: la primera de cada producto. Si alguno
            // llevaba dos, la otra se pierde, porque la columna no tiene dónde
            // ponerla.
            migrationBuilder.Sql(@"
                UPDATE ""Productos"" AS p
                SET ""IdSalsa"" = s.""IdReceta""
                FROM (
                    SELECT DISTINCT ON (""IdProducto"") ""IdProducto"", ""IdReceta""
                    FROM ""ProductoSalsas""
                    ORDER BY ""IdProducto"", ""Posicion"", ""IdReceta""
                ) AS s
                WHERE p.""IdProducto"" = s.""IdProducto"";");

            migrationBuilder.DropTable(
                name: "ProductoSalsas");
        }
    }
}
