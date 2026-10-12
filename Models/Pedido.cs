namespace f4872.Models;

public class Pedido
{
    public int IdPedido { get; set; }

    public string Cliente { get; set; } = null!;

    public string Telefono { get; set; } = null!;

    // todo pedido se entrega, no hay retiro, asi que la direccion es obligatoria
    public string Direccion { get; set; } = null!;

    // opcional: la usa una de las dos formas de pedir la direccion que quedan
    // sin decidir. Va igual para que esa decision no arrastre una migracion
    public string? Referencia { get; set; }

    // se guarda en UTC, que es lo que devuelve now() en Postgres. La conversion
    // a hora de Buenos Aires es para mostrar, y va con la pantalla que la muestre
    public DateTime FechaPedido { get; set; }

    // el dia de entrega, no el momento: la hora se arregla por WhatsApp. Nulo
    // mientras no se haya coordinado
    public DateOnly? FechaEntrega { get; set; }

    public EstadoPedido Estado { get; set; }

    public ICollection<ItemPedido> Items { get; set; } = new List<ItemPedido>();

    // ojo: suma lo que este cargado. Consultado sin Include da cero, que es un
    // numero creible y equivocado, asi que el total de una lista se pide en la
    // consulta y esto se usa cuando el pedido ya vino con sus items
    public decimal Total => Items.Sum(x => x.Total);

    public bool SinEntregar => Estado is EstadoPedido.Nuevo or EstadoPedido.Preparando;

    // el unico camino posible desde donde esta: nuevo va a preparando y
    // preparando a entregado. Desde entregado o cancelado no se sigue
    public EstadoPedido? Siguiente => Estado switch
    {
        EstadoPedido.Nuevo => EstadoPedido.Preparando,
        EstadoPedido.Preparando => EstadoPedido.Entregado,
        _ => null
    };
}
