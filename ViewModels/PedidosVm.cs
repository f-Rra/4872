using f4872.Helpers;
using f4872.Models;

namespace f4872.ViewModels;

public class PedidosVm : PanelVm
{
    // el filtro elegido, para marcar el chip encendido
    public string Filtro { get; set; } = Filtros.Activos;

    public IReadOnlyList<FilaPedido> Lista { get; set; } = [];

    // el de la derecha. Nulo cuando el filtro no deja ninguno a la vista
    public DetallePedido? Elegido { get; set; }

    public int SinAbrir { get; set; }
    public int EnTotal { get; set; }
}

// Los cinco chips de la cabecera. Van como texto y no como enum porque tres de
// ellos —activos, todos— no son estados: son maneras de mirar la lista.
public static class Filtros
{
    public const string Activos = "activos";
    public const string Nuevos = "nuevo";
    public const string Preparando = "prep";
    public const string Entregados = "entregado";
    public const string Todos = "todos";

    public static readonly (string Clave, string Nombre)[] Todo =
    [
        (Activos, "Activos"),
        (Nuevos, "Nuevos"),
        (Preparando, "Preparando"),
        (Entregados, "Entregados"),
        (Todos, "Todos")
    ];
}

// un renglón de la columna izquierda: alcanza para barrer la lista
public class FilaPedido
{
    public int IdPedido { get; set; }
    public string Cliente { get; set; } = null!;
    public EstadoPedido Estado { get; set; }
    public DateTime FechaPedido { get; set; }
    public decimal Total { get; set; }

    // La fecha se guarda en UTC y se muestra en hora de acá. El formateo vive
    // en el modelo de la vista y no en la vista para que las dos columnas —la
    // lista y el detalle— no puedan quedar diciendo horas distintas.
    public string Hora => Reloj.EnBuenosAires(FechaPedido).ToString("HH:mm");

    public string Numero => $"{IdPedido:0000}";
}

// el pedido entero, que es lo que se lee para prepararlo y para escribirle
public class DetallePedido
{
    public int IdPedido { get; set; }
    public string Cliente { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    public string Direccion { get; set; } = null!;
    public DateTime FechaPedido { get; set; }
    public EstadoPedido Estado { get; set; }
    public IReadOnlyList<ItemDelDetalle> Items { get; set; } = [];
    public decimal Total { get; set; }

    public string Numero => $"{IdPedido:0000}";

    // acá va la fecha entera y no solo la hora: un pedido del miércoles se lee
    // el sábado, y saber que entró «el 8, 23:28» cambia cuánto hace que espera
    public string Cuando => Reloj.EnBuenosAires(FechaPedido).ToString("dd/MM HH:mm");
}

public class ItemDelDetalle
{
    public int Cantidad { get; set; }
    public string Nombre { get; set; } = null!;

    // «sin albahaca, oliva», o vacío. Ya viene armado del modelo
    public string Sin { get; set; } = "";

    public decimal Total { get; set; }
}
