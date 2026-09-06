using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class PackConfiguracion : IEntityTypeConfiguration<Pack>
{
    public void Configure(EntityTypeBuilder<Pack> pack)
    {
        // clave propia por la convención del proyecto, aunque las unidades
        // alcanzarían: el índice único hace el mismo trabajo
        pack.HasKey(x => x.IdPack);

        pack.HasIndex(x => x.Unidades)
            .IsUnique();

        pack.Property(x => x.Precio)
            .HasPrecision(10, 2);

        pack.Property(x => x.Activo)
            .HasDefaultValue(true);

        // un pack de cero empanadas no es un pack
        pack.ToTable(t => t.HasCheckConstraint("CK_Packs_Unidades", "\"Unidades\" > 0"));
    }
}
