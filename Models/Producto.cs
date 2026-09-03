using System.ComponentModel;

namespace f4872.Models;

public class Producto
{
    public int IdProducto { get; set; }

    [DisplayName("Tipo")]
    public Familia Familia { get; set; }

    public string Nombre { get; set; } = null!;

    // las empanadas no tienen precio propio: se cobran por pack de 6 o de 12.
    // Va nulo de verdad y no un cero, que seria un precio que no es
    [DisplayName("Precio")]
    public decimal? Precio { get; set; }

    public bool Activo { get; set; } = true;

    // la sub-receta que consume: el bollo en pizzas y focaccias, la tapa en
    // empanadas. Una unidad por producto, que es como se arma
    public int? IdBase { get; set; }
    public Base? Base { get; set; }

    [DisplayName("Se cobra por pack")]
    public bool SeCobraPorPack => Familia == Familia.Empanada;

    [DisplayName("Estado")]
    public string Estado => Activo ? "En la carta" : "Se acabó";
}
