using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class ItemQuitadoConfiguracion : IEntityTypeConfiguration<ItemQuitado>
{
    public void Configure(EntityTypeBuilder<ItemQuitado> quitado)
    {
        // la clave es el par: no se puede sacar dos veces lo mismo del mismo item
        quitado.HasKey(x => new { x.IdItemPedido, x.Ingrediente });

        // el mismo largo que el nombre del ingrediente del que se copia
        quitado.Property(x => x.Ingrediente)
            .HasMaxLength(60)
            .IsRequired();

        quitado.HasOne(x => x.Item)
            .WithMany(x => x.Quitados)
            .HasForeignKey(x => x.IdItemPedido)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
