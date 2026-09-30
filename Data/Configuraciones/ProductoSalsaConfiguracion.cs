using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class ProductoSalsaConfiguracion : IEntityTypeConfiguration<ProductoSalsa>
{
    public void Configure(EntityTypeBuilder<ProductoSalsa> renglon)
    {
        // la clave es el par entero: la misma salsa no entra dos veces en la
        // misma pizza
        renglon.HasKey(x => new { x.IdProducto, x.IdReceta });

        renglon.Property(x => x.Porciones)
            .HasPrecision(6, 2)
            .HasDefaultValue(1m);

        renglon.Property(x => x.Quitable)
            .HasDefaultValue(false);

        // una salsa con cero porciones no es una salsa de la pizza: o la lleva
        // o no esta
        renglon.ToTable(t => t.HasCheckConstraint(
            "CK_ProductoSalsas_Porciones", "\"Porciones\" > 0"));

        // borrar un producto se lleva sus salsas, como se lleva su receta
        renglon.HasOne(x => x.Producto)
            .WithMany(x => x.Salsas)
            .HasForeignKey(x => x.IdProducto)
            .OnDelete(DeleteBehavior.Cascade);

        // borrar el pomodoro, en cambio, se frena: las pizzas que lo llevan no
        // pueden quedar apuntando a nada
        renglon.HasOne(x => x.Receta)
            .WithMany()
            .HasForeignKey(x => x.IdReceta)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
