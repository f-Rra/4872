using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class PedidoConfiguracion : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> pedido)
    {
        pedido.HasKey(x => x.IdPedido);

        pedido.Property(x => x.Cliente)
            .HasMaxLength(60)
            .IsRequired();

        pedido.Property(x => x.Telefono)
            .HasMaxLength(30)
            .IsRequired();

        pedido.Property(x => x.Direccion)
            .HasMaxLength(120)
            .IsRequired();

        pedido.Property(x => x.Referencia)
            .HasMaxLength(120);

        // el reloj lo pone Postgres y no la app: asi un pedido insertado a mano
        // tambien queda fechado, y la hora sale del mismo lugar siempre
        pedido.Property(x => x.FechaPedido)
            .HasDefaultValueSql("now()");

        pedido.Property(x => x.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}
