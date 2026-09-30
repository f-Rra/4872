using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class LlevarVariasSalsasConPorciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Porciones",
                table: "ProductoSalsas",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<bool>(
                name: "Quitable",
                table: "ProductoSalsas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductoSalsas_Porciones",
                table: "ProductoSalsas",
                sql: "\"Porciones\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductoSalsas_Porciones",
                table: "ProductoSalsas");

            migrationBuilder.DropColumn(
                name: "Porciones",
                table: "ProductoSalsas");

            migrationBuilder.DropColumn(
                name: "Quitable",
                table: "ProductoSalsas");
        }
    }
}
