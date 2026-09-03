using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class BaseIngredienteConfiguracion : IEntityTypeConfiguration<BaseIngrediente>
{
    public void Configure(EntityTypeBuilder<BaseIngrediente> renglon)
    {
        // la clave es el par, igual que en la receta de un producto: la harina
        // no puede aparecer dos veces en la receta del bollo
        renglon.HasKey(x => new { x.IdBase, x.IdIngrediente });

        renglon.Property(x => x.Cantidad)
            .HasPrecision(12, 3);

        renglon.ToTable(t => t.HasCheckConstraint(
            "CK_BaseIngredientes_Cantidad", "\"Cantidad\" > 0"));

        // borrar una base se lleva su receta, que sin la base no dice nada
        renglon.HasOne(x => x.Base)
            .WithMany(x => x.Receta)
            .HasForeignKey(x => x.IdBase)
            .OnDelete(DeleteBehavior.Cascade);

        // borrar un ingrediente en uso se frena: sin harina no hay bollo
        renglon.HasOne(x => x.Ingrediente)
            .WithMany(x => x.UsosEnBases)
            .HasForeignKey(x => x.IdIngrediente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
