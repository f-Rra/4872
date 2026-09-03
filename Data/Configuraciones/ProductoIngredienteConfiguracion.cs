using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class ProductoIngredienteConfiguracion : IEntityTypeConfiguration<ProductoIngrediente>
{
    public void Configure(EntityTypeBuilder<ProductoIngrediente> renglon)
    {
        // la clave es el par entero: asi el mismo ingrediente no puede entrar
        // dos veces en la misma receta y sumar dos cantidades distintas
        renglon.HasKey(x => new { x.IdProducto, x.IdIngrediente });

        renglon.Property(x => x.Cantidad)
            .HasPrecision(12, 3);

        renglon.Property(x => x.Quitable)
            .HasDefaultValue(false);

        // un ingrediente con cantidad cero no es un ingrediente de la receta:
        // o lleva algo o no esta
        renglon.ToTable(t => t.HasCheckConstraint(
            "CK_ProductoIngredientes_Cantidad", "\"Cantidad\" > 0"));

        // borrar un producto se lleva su receta, que sin el producto no dice nada
        renglon.HasOne(x => x.Producto)
            .WithMany(x => x.Receta)
            .HasForeignKey(x => x.IdProducto)
            .OnDelete(DeleteBehavior.Cascade);

        // borrar un ingrediente en uso, en cambio, se frena: primero hay que
        // sacarlo de las recetas, si no las pizzas quedan sin la mitad
        renglon.HasOne(x => x.Ingrediente)
            .WithMany(x => x.Usos)
            .HasForeignKey(x => x.IdIngrediente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
