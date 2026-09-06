namespace f4872.ViewModels;

// un renglón de la carta, ya resuelto: la vista no navega entidades ni sabe de EF
public class RenglonCarta
{
    public string Nombre { get; set; } = null!;
    public decimal? Precio { get; set; }
    public bool Activo { get; set; }
    public IReadOnlyList<string> Ingredientes { get; set; } = [];
}

public class CartaVm
{
    // pizzas y focaccias comparten renglón porque se piden igual, pero van en
    // solapas distintas: son dos listas y no una sola con un filtro
    public IReadOnlyList<RenglonCarta> Pizzas { get; set; } = [];
    public IReadOnlyList<RenglonCarta> Focaccias { get; set; } = [];
}
