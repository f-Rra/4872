using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class OrdenarLosIngredientesDeCadaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Posicion",
                table: "ProductoIngredientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Cada receta arranca en el orden que ya tiene en la carta, que es
            // el de IdIngrediente: así la tienda no cambia al publicar. Lo que
            // cambia es la ficha del panel, que hasta acá iba por nombre.
            migrationBuilder.Sql(@"
                UPDATE ""ProductoIngredientes"" AS r
                SET ""Posicion"" = x.n
                FROM (
                    SELECT ""IdProducto"", ""IdIngrediente"",
                           row_number() OVER (PARTITION BY ""IdProducto"" ORDER BY ""IdIngrediente"") AS n
                    FROM ""ProductoIngredientes""
                ) AS x
                WHERE r.""IdProducto"" = x.""IdProducto"" AND r.""IdIngrediente"" = x.""IdIngrediente"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Posicion",
                table: "ProductoIngredientes");
        }
    }
}
