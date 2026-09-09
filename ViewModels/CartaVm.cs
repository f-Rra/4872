namespace f4872.ViewModels;

// un ingrediente del renglón. El quitable sale del par producto-ingrediente:
// es del par y no del ingrediente, porque la misma cebolla es quitable en una
// fugazzeta y no lo es en otra receta
public class IngredienteCarta
{
    public int IdIngrediente { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Quitable { get; set; }
}

// un renglón de la carta, ya resuelto: la vista no navega entidades ni sabe de EF
public class RenglonCarta
{
    // el contador se identifica por id y no por nombre: es lo que despues viaja
    // al pedido, y un nombre puede cambiar sin que cambie el producto
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = null!;
    public decimal? Precio { get; set; }
    public bool Activo { get; set; }
    public IReadOnlyList<IngredienteCarta> Ingredientes { get; set; } = [];
}

// las empanadas van solo con el nombre: el precio no es del gusto, es del pack
public class Gusto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; }
}

public class TamanoPack
{
    public int Unidades { get; set; }
    public decimal Precio { get; set; }
}

public class CartaVm
{
    // Cerrada, la pantalla no muestra la carta, así que las cuatro listas
    // llegan vacías: el controlador ni siquiera las consulta.
    public bool Abierta { get; set; } = true;

    // pizzas y focaccias comparten renglón porque se piden igual, pero van en
    // solapas distintas: son dos listas y no una sola con un filtro
    public IReadOnlyList<RenglonCarta> Pizzas { get; set; } = [];
    public IReadOnlyList<RenglonCarta> Focaccias { get; set; } = [];

    public IReadOnlyList<Gusto> Gustos { get; set; } = [];
    public IReadOnlyList<TamanoPack> Packs { get; set; } = [];
}
