using System.ComponentModel;

namespace f4872.Models;

public class Pedido
{
    public int IdPedido { get; set; }

    [DisplayName("Cliente")]
    public string Cliente { get; set; } = null!;

    [DisplayName("WhatsApp")]
    public string Telefono { get; set; } = null!;

    // todo pedido se entrega, no hay retiro, asi que la direccion es obligatoria
    [DisplayName("Dirección")]
    public string Direccion { get; set; } = null!;

    // opcional: la usa una de las dos formas de pedir la direccion que quedan
    // sin decidir. Va igual para que esa decision no arrastre una migracion
    [DisplayName("Piso, timbre o referencia")]
    public string? Referencia { get; set; }

    // se guarda en UTC, que es lo que devuelve now() en Postgres. La conversion
    // a hora de Buenos Aires es para mostrar, y va con la pantalla que la muestre
    [DisplayName("Fecha del pedido")]
    public DateTime FechaPedido { get; set; }

    // el dia de entrega, no el momento: la hora se arregla por WhatsApp. Nulo
    // mientras no se haya coordinado
    [DisplayName("Fecha de entrega")]
    public DateOnly? FechaEntrega { get; set; }

    [DisplayName("Estado")]
    public EstadoPedido Estado { get; set; }

    public ICollection<ItemPedido> Items { get; set; } = new List<ItemPedido>();

    // ojo: suma lo que este cargado. Consultado sin Include da cero, que es un
    // numero creible y equivocado, asi que el total de una lista se pide en la
    // consulta y esto se usa cuando el pedido ya vino con sus items
    [DisplayName("Total")]
    public decimal Total => Items.Sum(x => x.Total);

    // el cancelado sale de los calculos pero queda en el historial
    [DisplayName("Cuenta")]
    public bool Cuenta => Estado != EstadoPedido.Cancelado;

    [DisplayName("Sin entregar")]
    public bool SinEntregar => Estado is EstadoPedido.Nuevo or EstadoPedido.Preparando;

    // el unico camino posible desde donde esta: nuevo va a preparando y
    // preparando a entregado. Desde entregado o cancelado no se sigue
    [DisplayName("Siguiente estado")]
    public EstadoPedido? Siguiente => Estado switch
    {
        EstadoPedido.Nuevo => EstadoPedido.Preparando,
        EstadoPedido.Preparando => EstadoPedido.Entregado,
        _ => null
    };
}
