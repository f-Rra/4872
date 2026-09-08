namespace f4872.ViewModels;

// La carta que el checkout necesita para poder leer el pedido guardado.
//
// El pedido vive en el navegador como claves y cantidades: "p12" son doce
// unidades de nada si no se sabe que 12 es la Margarita y cuánto sale. Esto es
// el diccionario que traduce esas claves, y viaja como JSON adentro de la
// pantalla en vez de por una consulta aparte: es la misma carta que el
// servidor ya tiene a mano cuando arma la vista.
public class CheckoutVm
{
    // pizzas y focaccias: la clave de un renglón es "p" + este id
    public IReadOnlyDictionary<int, ProductoDelPedido> Productos { get; set; } =
        new Dictionary<int, ProductoDelPedido>();

    // los gustos de empanada: la clave de un pack es "e" + este id + "x" + unidades
    public IReadOnlyDictionary<int, string> Gustos { get; set; } = new Dictionary<int, string>();

    // cuánto sale cada tamaño de pack, por unidades
    public IReadOnlyDictionary<int, decimal> Packs { get; set; } = new Dictionary<int, decimal>();

    // solo los quitables: son los únicos que pueden aparecer en un «sin»
    public IReadOnlyDictionary<int, string> Ingredientes { get; set; } = new Dictionary<int, string>();
}

public class ProductoDelPedido
{
    public string Nombre { get; set; } = null!;
    public decimal Precio { get; set; }
    // para ordenar el resumen igual que la carta, y no por orden de agregado
    public int Orden { get; set; }
}
