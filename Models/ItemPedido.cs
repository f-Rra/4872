namespace f4872.Models;

public class ItemPedido
{
    public int IdItemPedido { get; set; }

    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    // cuantos de este renglon: dos pizzas, o dos packs de empanadas
    public int Cantidad { get; set; }

    // 6 o 12 en las empanadas, nulo en todo lo demas. Es lo que separa "dos
    // packs" de "dos pizzas" a la hora de contar lo que hay que hornear
    public int? UnidadesPorPack { get; set; }

    // el precio se COPIA del producto al confirmar y no se lee mas de ahi: si
    // en marzo la muzza salia $9.000 y hoy sale $11.000, el pedido de marzo
    // tiene que seguir diciendo $9.000. En las empanadas es el precio del pack
    public decimal PrecioUnitario { get; set; }

    public ICollection<ItemQuitado> Quitados { get; set; } = new List<ItemQuitado>();

    // "sin albahaca, oliva", igual que en la maqueta. Va ordenado alfabeticamente
    // a proposito: dos items con el mismo sin tienen que dar el mismo texto, que
    // es lo que despues permite agruparlos en un solo renglon del resumen.
    // Necesita los quitados cargados: sin Include da vacio, que se lee como
    // "no sacaron nada"
    public string Sin => Quitados.Count == 0
        ? ""
        : "sin " + string.Join(", ", Quitados.Select(x => x.Ingrediente).OrderBy(x => x));

    public decimal Total => Cantidad * PrecioUnitario;
}
