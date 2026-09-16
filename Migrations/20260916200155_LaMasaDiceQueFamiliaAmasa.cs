using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    /// <inheritdoc />
    public partial class LaMasaDiceQueFamiliaAmasa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Familia",
                table: "Bases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Las masas que ya estaban sembradas quedan todas en "", y el indice
            // unico de abajo no se puede crear con dos filas iguales. Asi que
            // primero se llena la columna.
            //
            // Sale de los productos que ya la usan, que es de donde lo sacaba
            // BaseDe hasta ahora: el dato ya estaba, solo que disperso.
            migrationBuilder.Sql(@"
                UPDATE ""Bases"" b
                SET ""Familia"" = s.""Familia""
                FROM (
                    SELECT DISTINCT ON (p.""IdBase"")
                           p.""IdBase"", p.""Familia"", count(*) AS n
                    FROM ""Productos"" p
                    WHERE p.""IdBase"" IS NOT NULL
                    GROUP BY p.""IdBase"", p.""Familia""
                    ORDER BY p.""IdBase"", n DESC
                ) s
                WHERE s.""IdBase"" = b.""IdBase"";");

            // y si alguna no la usa ningun producto todavia, por el nombre
            migrationBuilder.Sql(@"
                UPDATE ""Bases"" SET ""Familia"" = 'Pizza'
                WHERE ""Familia"" = '' AND ""Nombre"" ILIKE '%pizza%';
                UPDATE ""Bases"" SET ""Familia"" = 'Focaccia'
                WHERE ""Familia"" = '' AND ""Nombre"" ILIKE '%focaccia%';");

            migrationBuilder.CreateIndex(
                name: "IX_Bases_Familia",
                table: "Bases",
                column: "Familia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bases_Familia",
                table: "Bases");

            migrationBuilder.DropColumn(
                name: "Familia",
                table: "Bases");
        }
    }
}
