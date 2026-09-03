using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class RelacionarProductoConIngredientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductoIngredientes",
                columns: table => new
                {
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    IdIngrediente = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Quitable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoIngredientes", x => new { x.IdProducto, x.IdIngrediente });
                    table.CheckConstraint("CK_ProductoIngredientes_Cantidad", "\"Cantidad\" > 0");
                    table.ForeignKey(
                        name: "FK_ProductoIngredientes_Ingredientes_IdIngrediente",
                        column: x => x.IdIngrediente,
                        principalTable: "Ingredientes",
                        principalColumn: "IdIngrediente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductoIngredientes_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoIngredientes_IdIngrediente",
                table: "ProductoIngredientes",
                column: "IdIngrediente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductoIngredientes");
        }
    }
}
