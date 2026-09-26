using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class ElegirLaPosicionDeCadaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Posicion",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Los que ya están quedan en el orden de hoy, que es el de alta:
            // se numeran de 1 en adelante dentro de cada familia.
            migrationBuilder.Sql(@"
                UPDATE ""Productos"" AS p
                SET ""Posicion"" = x.n
                FROM (
                    SELECT ""IdProducto"",
                           row_number() OVER (PARTITION BY ""Familia"" ORDER BY ""IdProducto"") AS n
                    FROM ""Productos""
                ) AS x
                WHERE p.""IdProducto"" = x.""IdProducto"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Posicion",
                table: "Productos");
        }
    }
}
