using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class RelacionarBaseConIngredientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaseIngredientes",
                columns: table => new
                {
                    IdBase = table.Column<int>(type: "integer", nullable: false),
                    IdIngrediente = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientes", x => new { x.IdBase, x.IdIngrediente });
                    table.CheckConstraint("CK_BaseIngredientes_Cantidad", "\"Cantidad\" > 0");
                    table.ForeignKey(
                        name: "FK_BaseIngredientes_Bases_IdBase",
                        column: x => x.IdBase,
                        principalTable: "Bases",
                        principalColumn: "IdBase",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseIngredientes_Ingredientes_IdIngrediente",
                        column: x => x.IdIngrediente,
                        principalTable: "Ingredientes",
                        principalColumn: "IdIngrediente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientes_IdIngrediente",
                table: "BaseIngredientes",
                column: "IdIngrediente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseIngredientes");
        }
    }
}
