using System.ComponentModel;

namespace f4872.Models;

public class ItemPedido
{
    public int IdItemPedido { get; set; }

    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    // cuantos de este renglon: dos pizzas, o dos packs de empanadas
    [DisplayName("Cantidad")]
    public int Cantidad { get; set; }

    // 6 o 12 en las empanadas, nulo en todo lo demas. Es lo que separa "dos
    // packs" de "dos pizzas" a la hora de contar lo que hay que hornear
    [DisplayName("Unidades por pack")]
    public int? UnidadesPorPack { get; set; }

    // el precio se COPIA del producto al confirmar y no se lee mas de ahi: si
    // en marzo la muzza salia $9.000 y hoy sale $11.000, el pedido de marzo
    // tiene que seguir diciendo $9.000. En las empanadas es el precio del pack
    [DisplayName("Precio unitario")]
    public decimal PrecioUnitario { get; set; }

    [DisplayName("Total")]
    public decimal Total => Cantidad * PrecioUnitario;

    // las piezas que hay que hacer: un pack de 12 son doce empanadas, y el
    // panel de produccion cuenta empanadas, no packs
    [DisplayName("Unidades")]
    public int Unidades => Cantidad * (UnidadesPorPack ?? 1);
}
