using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class ArmarCadaEmpanadaConSuRelleno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdRelleno",
                table: "Productos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdRelleno",
                table: "Productos",
                column: "IdRelleno");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Recetas_IdRelleno",
                table: "Productos",
                column: "IdRelleno",
                principalTable: "Recetas",
                principalColumn: "IdReceta",
                onDelete: ReferentialAction.Restrict);

            // Lo que ya estaba cargado no se pierde. Una empanada con
            // ingredientes sueltos pasa a tener un relleno con esos mismos
            // ingredientes, cargado para 12 como se hace: lo de una empanada
            // por 12. Así lo que cuesta una da exactamente igual que antes.
            //
            // Se llama como la empanada. Si ya hay una receta con ese nombre
            // -un relleno cargado a mano en Recetas-, se le agrega «(relleno)»,
            // y si también estuviera, el número del producto: el índice único
            // no puede frenar la migración, porque sin ella la app no arranca.
            // El vendedor decide después con cuál se queda.
            migrationBuilder.Sql(@"
                WITH empanadas AS (
                    SELECT p.""IdProducto"",
                           CASE
                               WHEN NOT EXISTS (SELECT 1 FROM ""Recetas"" r WHERE r.""Nombre"" = p.""Nombre"")
                                   THEN p.""Nombre""
                               WHEN NOT EXISTS (SELECT 1 FROM ""Recetas"" r WHERE r.""Nombre"" = left(p.""Nombre"", 49) || ' (relleno)')
                                   THEN left(p.""Nombre"", 49) || ' (relleno)'
                               ELSE left(p.""Nombre"", 40) || ' (relleno ' || p.""IdProducto"" || ')'
                           END AS ""Nombre""
                    FROM ""Productos"" p
                    WHERE p.""Familia"" = 'Empanada'
                      AND EXISTS (SELECT 1 FROM ""ProductoIngredientes"" x WHERE x.""IdProducto"" = p.""IdProducto"")
                ),
                nuevos AS (
                    INSERT INTO ""Recetas"" (""Nombre"", ""Tipo"", ""Familia"", ""Rinde"")
                    SELECT ""Nombre"", 'Relleno', NULL, 12 FROM empanadas
                    RETURNING ""IdReceta"", ""Nombre""
                )
                UPDATE ""Productos"" p
                SET ""IdRelleno"" = n.""IdReceta""
                FROM empanadas e
                JOIN nuevos n ON n.""Nombre"" = e.""Nombre""
                WHERE p.""IdProducto"" = e.""IdProducto"";

                INSERT INTO ""RecetaIngredientes"" (""IdReceta"", ""IdIngrediente"", ""Cantidad"")
                SELECT p.""IdRelleno"", x.""IdIngrediente"", x.""Cantidad"" * 12
                FROM ""ProductoIngredientes"" x
                JOIN ""Productos"" p ON p.""IdProducto"" = x.""IdProducto""
                WHERE p.""Familia"" = 'Empanada' AND p.""IdRelleno"" IS NOT NULL;

                DELETE FROM ""ProductoIngredientes"" x
                USING ""Productos"" p
                WHERE p.""IdProducto"" = x.""IdProducto""
                  AND p.""Familia"" = 'Empanada' AND p.""IdRelleno"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La vuelta: cada empanada recupera como ingredientes sueltos lo que
            // le toca a una de su relleno. Los rellenos quedan en Recetas, que
            // ya existían antes de esta migración.
            migrationBuilder.Sql(@"
                INSERT INTO ""ProductoIngredientes"" (""IdProducto"", ""IdIngrediente"", ""Cantidad"", ""Quitable"")
                SELECT p.""IdProducto"", r.""IdIngrediente"", r.""Cantidad"" / rec.""Rinde"", false
                FROM ""Productos"" p
                JOIN ""Recetas"" rec ON rec.""IdReceta"" = p.""IdRelleno""
                JOIN ""RecetaIngredientes"" r ON r.""IdReceta"" = rec.""IdReceta""
                WHERE p.""Familia"" = 'Empanada'
                ON CONFLICT DO NOTHING;");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Recetas_IdRelleno",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdRelleno",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdRelleno",
                table: "Productos");
        }
    }
}
