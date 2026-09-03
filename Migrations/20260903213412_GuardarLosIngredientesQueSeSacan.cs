using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class GuardarLosIngredientesQueSeSacan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemQuitados",
                columns: table => new
                {
                    IdItemPedido = table.Column<int>(type: "integer", nullable: false),
                    Ingrediente = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemQuitados", x => new { x.IdItemPedido, x.Ingrediente });
                    table.ForeignKey(
                        name: "FK_ItemQuitados_ItemPedidos_IdItemPedido",
                        column: x => x.IdItemPedido,
                        principalTable: "ItemPedidos",
                        principalColumn: "IdItemPedido",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemQuitados");
        }
    }
}
