using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class CrearTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tienda",
                columns: table => new
                {
                    IdTienda = table.Column<int>(type: "integer", nullable: false),
                    Abierta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tienda", x => x.IdTienda);
                    table.CheckConstraint("CK_Tienda_UnaSolaFila", "\"IdTienda\" = 1");
                });

            migrationBuilder.InsertData(
                table: "Tienda",
                columns: new[] { "IdTienda", "Abierta" },
                values: new object[] { 1, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tienda");
        }
    }
}
