using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class RecetaConfiguracion : IEntityTypeConfiguration<Receta>
{
    public void Configure(EntityTypeBuilder<Receta> receta)
    {
        receta.HasKey(x => x.IdReceta);

        receta.Property(x => x.Nombre)
            .HasMaxLength(60)
            .IsRequired();

        receta.HasIndex(x => x.Nombre)
            .IsUnique();

        // escrito y no como numero, por lo mismo que la familia del producto
        receta.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        receta.Property(x => x.Familia)
            .HasConversion<string>()
            .HasMaxLength(20);

        // una sola masa por familia: si hubiera dos, cual amasa una pizza nueva
        // volveria a ser una adivinanza. Las salsas y los rellenos la dejan
        // nula, y en Postgres dos nulos no chocan en un indice unico
        receta.HasIndex(x => x.Familia)
            .IsUnique();

        receta.Property(x => x.Rinde)
            .HasDefaultValue(1);

        receta.ToTable(t =>
        {
            // todo el calculo de una receta es dividir por el rinde: un cero acá
            // seria una division por cero, y un negativo un costo negativo
            t.HasCheckConstraint("CK_Recetas_Rinde", "\"Rinde\" > 0");

            // la familia es de las bases y solo de ellas: una base sin familia
            // deja a la pizza nueva sin masa, y una salsa con familia no quiere
            // decir nada
            t.HasCheckConstraint("CK_Recetas_Familia", "(\"Tipo\" = 'Base') = (\"Familia\" IS NOT NULL)");
        });
    }
}
