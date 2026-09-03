using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class BaseConfiguracion : IEntityTypeConfiguration<Base>
{
    public void Configure(EntityTypeBuilder<Base> laBase)
    {
        laBase.HasKey(x => x.IdBase);

        laBase.Property(x => x.Nombre)
            .HasMaxLength(60)
            .IsRequired();

        laBase.HasIndex(x => x.Nombre)
            .IsUnique();

        laBase.Property(x => x.Rinde)
            .HasDefaultValue(1);

        // todo el calculo de la base es dividir por el rinde: un cero acá seria
        // una division por cero, y un negativo un costo negativo
        laBase.ToTable(t => t.HasCheckConstraint("CK_Bases_Rinde", "\"Rinde\" > 0"));
    }
}
