using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace f4872.Migrations
{
    // Escrita a mano. La que arma EF solo borra las dos tablas y las vuelve a
    // crear vacías: en producción eso se lleva los bollos que él cargó, y los
    // productos quedan apuntando a una masa que no existe. Renombrando, cada
    // fila queda donde estaba, con su mismo id y sus mismos números.
    /// <inheritdoc />
    public partial class ConvertirLasBasesEnRecetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Bases", newName: "Recetas");
            migrationBuilder.RenameColumn(name: "IdBase", table: "Recetas", newName: "IdReceta");
            migrationBuilder.RenameIndex(name: "IX_Bases_Nombre", table: "Recetas", newName: "IX_Recetas_Nombre");
            migrationBuilder.RenameIndex(name: "IX_Bases_Familia", table: "Recetas", newName: "IX_Recetas_Familia");

            migrationBuilder.RenameTable(name: "BaseIngredientes", newName: "RecetaIngredientes");
            migrationBuilder.RenameColumn(name: "IdBase", table: "RecetaIngredientes", newName: "IdReceta");
            migrationBuilder.RenameIndex(name: "IX_BaseIngredientes_IdIngrediente", table: "RecetaIngredientes",
                newName: "IX_RecetaIngredientes_IdIngrediente");

            // Las restricciones no cambian de nombre con la tabla, y la próxima
            // migración que toque alguna la va a buscar por el nombre nuevo. EF
            // no tiene una operación para renombrarlas, así que va en SQL.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Recetas"" RENAME CONSTRAINT ""PK_Bases"" TO ""PK_Recetas"";
                ALTER TABLE ""Recetas"" RENAME CONSTRAINT ""CK_Bases_Rinde"" TO ""CK_Recetas_Rinde"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""PK_BaseIngredientes"" TO ""PK_RecetaIngredientes"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""CK_BaseIngredientes_Cantidad"" TO ""CK_RecetaIngredientes_Cantidad"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""FK_BaseIngredientes_Bases_IdBase"" TO ""FK_RecetaIngredientes_Recetas_IdReceta"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""FK_BaseIngredientes_Ingredientes_IdIngrediente"" TO ""FK_RecetaIngredientes_Ingredientes_IdIngrediente"";
                ALTER TABLE ""Productos"" RENAME CONSTRAINT ""FK_Productos_Bases_IdBase"" TO ""FK_Productos_Recetas_IdBase"";");

            // Las que ya estaban son todas bases. El valor por defecto es solo
            // para llenarlas y se saca enseguida: de acá en más el tipo lo dice,
            // siempre, el que crea la receta.
            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Recetas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Base");

            migrationBuilder.Sql(@"ALTER TABLE ""Recetas"" ALTER COLUMN ""Tipo"" DROP DEFAULT;");

            // Una salsa o un relleno no se amasan, así que la familia puede
            // quedar vacía. El '' por defecto venía de cuando se agregó la
            // columna, y ahora chocaría con la regla de abajo.
            migrationBuilder.AlterColumn<string>(
                name: "Familia",
                table: "Recetas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql(@"ALTER TABLE ""Recetas"" ALTER COLUMN ""Familia"" DROP DEFAULT;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recetas_Familia",
                table: "Recetas",
                sql: "(\"Tipo\" = 'Base') = (\"Familia\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_Recetas_Familia", table: "Recetas");

            // Volver atrás pierde las salsas y los rellenos: la tabla de antes
            // era solo de bases y no tiene dónde guardarlos. Sus ingredientes
            // se van con ellos, por la cascada.
            migrationBuilder.Sql(@"DELETE FROM ""Recetas"" WHERE ""Tipo"" <> 'Base';");

            migrationBuilder.DropColumn(name: "Tipo", table: "Recetas");

            migrationBuilder.AlterColumn<string>(
                name: "Familia",
                table: "Recetas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.Sql(@"
                ALTER TABLE ""Productos"" RENAME CONSTRAINT ""FK_Productos_Recetas_IdBase"" TO ""FK_Productos_Bases_IdBase"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""FK_RecetaIngredientes_Ingredientes_IdIngrediente"" TO ""FK_BaseIngredientes_Ingredientes_IdIngrediente"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""FK_RecetaIngredientes_Recetas_IdReceta"" TO ""FK_BaseIngredientes_Bases_IdBase"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""CK_RecetaIngredientes_Cantidad"" TO ""CK_BaseIngredientes_Cantidad"";
                ALTER TABLE ""RecetaIngredientes"" RENAME CONSTRAINT ""PK_RecetaIngredientes"" TO ""PK_BaseIngredientes"";
                ALTER TABLE ""Recetas"" RENAME CONSTRAINT ""CK_Recetas_Rinde"" TO ""CK_Bases_Rinde"";
                ALTER TABLE ""Recetas"" RENAME CONSTRAINT ""PK_Recetas"" TO ""PK_Bases"";");

            migrationBuilder.RenameIndex(name: "IX_RecetaIngredientes_IdIngrediente", table: "RecetaIngredientes",
                newName: "IX_BaseIngredientes_IdIngrediente");
            migrationBuilder.RenameColumn(name: "IdReceta", table: "RecetaIngredientes", newName: "IdBase");
            migrationBuilder.RenameTable(name: "RecetaIngredientes", newName: "BaseIngredientes");

            migrationBuilder.RenameIndex(name: "IX_Recetas_Familia", table: "Recetas", newName: "IX_Bases_Familia");
            migrationBuilder.RenameIndex(name: "IX_Recetas_Nombre", table: "Recetas", newName: "IX_Bases_Nombre");
            migrationBuilder.RenameColumn(name: "IdReceta", table: "Recetas", newName: "IdBase");
            migrationBuilder.RenameTable(name: "Recetas", newName: "Bases");
        }
    }
}
