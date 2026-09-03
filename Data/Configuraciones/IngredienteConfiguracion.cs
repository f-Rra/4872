using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class IngredienteConfiguracion : IEntityTypeConfiguration<Ingrediente>
{
    public void Configure(EntityTypeBuilder<Ingrediente> ingrediente)
    {
        ingrediente.HasKey(x => x.IdIngrediente);

        ingrediente.Property(x => x.Nombre)
            .HasMaxLength(60)
            .IsRequired();

        // escrita y no como numero, por lo mismo que la familia del producto
        ingrediente.Property(x => x.Unidad)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // tres decimales alcanzan para cualquier gramaje y dejan lugar a media
        // unidad, que es lo mas chico que se puede pedir de algo que se cuenta
        ingrediente.Property(x => x.Stock)
            .HasPrecision(12, 3);

        ingrediente.Property(x => x.CantidadDeCompra)
            .HasPrecision(12, 3);

        ingrediente.Property(x => x.PrecioDeCompra)
            .HasPrecision(10, 2);

        ingrediente.Property(x => x.Libre)
            .HasDefaultValue(false);

        // el mismo ingrediente escrito de dos formas rompe los costos sin avisar:
        // la cebolla entraria dos veces y ninguna de las dos tendria el total
        ingrediente.HasIndex(x => x.Nombre)
            .IsUnique();
    }
}
