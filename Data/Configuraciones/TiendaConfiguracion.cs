using f4872.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace f4872.Data.Configuraciones;

public class TiendaConfiguracion : IEntityTypeConfiguration<Tienda>
{
    public void Configure(EntityTypeBuilder<Tienda> tienda)
    {
        tienda.HasKey(x => x.IdTienda);

        // la clave no se genera: la fila es una sola y siempre es la 1
        tienda.Property(x => x.IdTienda)
            .ValueGeneratedNever();

        // Una segunda fila sería un segundo estado, y la tienda estaría abierta
        // y cerrada a la vez. La restricción lo hace imposible en la base y no
        // solo en el código.
        tienda.ToTable(t => t.HasCheckConstraint("CK_Tienda_UnaSolaFila", "\"IdTienda\" = 1"));

        // La fila viaja en la migración y no en el Sembrador: el sembrador solo
        // corre en Development y con datos inventados, y esta fila tiene que
        // existir también en producción, donde no hay nadie que la cree.
        tienda.HasData(new Tienda { IdTienda = 1, Abierta = true });
    }
}
