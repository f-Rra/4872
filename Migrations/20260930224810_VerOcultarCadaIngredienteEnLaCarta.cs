using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class VerOcultarCadaIngredienteEnLaCarta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Visible",
                table: "ProductoSalsas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Visible",
                table: "ProductoIngredientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Hasta hoy la carta nombraba todo lo que llevaba una receta. Ahora
            // nombra como mucho cuatro, así que de cada producto se dejan
            // visibles los primeros cuatro, contando ingredientes y salsas
            // juntos y en el orden de la carta: por lugar y, si empatan, la
            // salsa primero. Los que quedan afuera no pueden ser modificables,
            // porque el cliente ya no sabe que están.
            //
            // Si ningún producto pasaba de cuatro, esto no cambia nada.
            var rango = @"
                WITH rango AS (
                    SELECT ""IdProducto"", tipo, id,
                           row_number() OVER (PARTITION BY ""IdProducto"" ORDER BY ""Posicion"", orden, id) AS n
                    FROM (
                        SELECT ""IdProducto"", 'i' AS tipo, 1 AS orden, ""IdIngrediente"" AS id, ""Posicion""
                        FROM ""ProductoIngredientes""
                        UNION ALL
                        SELECT ""IdProducto"", 's', 0, ""IdReceta"", ""Posicion""
                        FROM ""ProductoSalsas""
                    ) AS todos
                )";

            migrationBuilder.Sql(rango + @"
                UPDATE ""ProductoIngredientes"" AS p
                SET ""Visible"" = false, ""Quitable"" = false
                FROM rango AS r
                WHERE r.tipo = 'i' AND r.n > 4
                  AND r.""IdProducto"" = p.""IdProducto"" AND r.id = p.""IdIngrediente"";");

            migrationBuilder.Sql(rango + @"
                UPDATE ""ProductoSalsas"" AS p
                SET ""Visible"" = false, ""Quitable"" = false
                FROM rango AS r
                WHERE r.tipo = 's' AND r.n > 4
                  AND r.""IdProducto"" = p.""IdProducto"" AND r.id = p.""IdReceta"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visible",
                table: "ProductoSalsas");

            migrationBuilder.DropColumn(
                name: "Visible",
                table: "ProductoIngredientes");
        }
    }
}
