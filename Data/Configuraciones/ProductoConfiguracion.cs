using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> producto)
    {
        producto.HasKey(x => x.IdProducto);

        // la familia se guarda escrita y no como numero: en pgAdmin se lee "Pizza"
        // y no "0", y el dia que se agregue una familia al medio del enum las filas
        // viejas siguen queriendo decir lo mismo
        producto.Property(x => x.Familia)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        producto.Property(x => x.Nombre)
            .HasMaxLength(60)
            .IsRequired();

        producto.Property(x => x.Precio)
            .HasPrecision(10, 2);

        producto.Property(x => x.Activo)
            .HasDefaultValue(true);

        // dos productos con el mismo nombre en la misma familia son el mismo producto
        producto.HasIndex(x => new { x.Familia, x.Nombre })
            .IsUnique();
    }
}
