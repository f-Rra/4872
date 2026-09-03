using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class ItemPedidoConfiguracion : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> item)
    {
        // clave propia y no el par pedido-producto: un mismo pedido puede llevar
        // una margarita normal y otra sin albahaca, y son dos renglones distintos
        item.HasKey(x => x.IdItemPedido);

        item.Property(x => x.PrecioUnitario)
            .HasPrecision(10, 2);

        item.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ItemPedidos_Cantidad", "\"Cantidad\" > 0");
            t.HasCheckConstraint("CK_ItemPedidos_Precio", "\"PrecioUnitario\" >= 0");
            t.HasCheckConstraint("CK_ItemPedidos_Pack",
                "\"UnidadesPorPack\" IS NULL OR \"UnidadesPorPack\" > 0");
        });

        // borrar un pedido se lleva sus items, que sin el pedido no dicen nada
        item.HasOne(x => x.Pedido)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.IdPedido)
            .OnDelete(DeleteBehavior.Cascade);

        // sin navegacion del lado del producto: nadie necesita pedirle a una
        // pizza la lista de todos los pedidos donde salio
        item.HasOne(x => x.Producto)
            .WithMany()
            .HasForeignKey(x => x.IdProducto)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
