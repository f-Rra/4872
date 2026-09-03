using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class GuardarLosItemsDelPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemPedidos",
                columns: table => new
                {
                    IdItemPedido = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdPedido = table.Column<int>(type: "integer", nullable: false),
                    IdProducto = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    UnidadesPorPack = table.Column<int>(type: "integer", nullable: true),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPedidos", x => x.IdItemPedido);
                    table.CheckConstraint("CK_ItemPedidos_Cantidad", "\"Cantidad\" > 0");
                    table.CheckConstraint("CK_ItemPedidos_Pack", "\"UnidadesPorPack\" IS NULL OR \"UnidadesPorPack\" > 0");
                    table.CheckConstraint("CK_ItemPedidos_Precio", "\"PrecioUnitario\" >= 0");
                    table.ForeignKey(
                        name: "FK_ItemPedidos_Pedidos_IdPedido",
                        column: x => x.IdPedido,
                        principalTable: "Pedidos",
                        principalColumn: "IdPedido",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemPedidos_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemPedidos_IdPedido",
                table: "ItemPedidos",
                column: "IdPedido");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPedidos_IdProducto",
                table: "ItemPedidos",
                column: "IdProducto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemPedidos");
        }
    }
}
