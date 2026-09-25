using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class RecetaIngredienteConfiguracion : IEntityTypeConfiguration<RecetaIngrediente>
{
    public void Configure(EntityTypeBuilder<RecetaIngrediente> renglon)
    {
        // la clave es el par, igual que en la receta de un producto: la harina
        // no puede aparecer dos veces en la receta del bollo
        renglon.HasKey(x => new { x.IdReceta, x.IdIngrediente });

        renglon.Property(x => x.Cantidad)
            .HasPrecision(12, 3);

        renglon.ToTable(t => t.HasCheckConstraint(
            "CK_RecetaIngredientes_Cantidad", "\"Cantidad\" > 0"));

        // borrar una receta se lleva sus ingredientes, que sin ella no dicen nada
        renglon.HasOne(x => x.Receta)
            .WithMany(x => x.Ingredientes)
            .HasForeignKey(x => x.IdReceta)
            .OnDelete(DeleteBehavior.Cascade);

        // borrar un ingrediente en uso se frena: sin harina no hay bollo
        renglon.HasOne(x => x.Ingrediente)
            .WithMany(x => x.UsosEnRecetas)
            .HasForeignKey(x => x.IdIngrediente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
