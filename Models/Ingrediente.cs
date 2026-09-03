using System.ComponentModel;

namespace f4872.Models;

public class Ingrediente
{
    public int IdIngrediente { get; set; }

    public string Nombre { get; set; } = null!;

    public Medida Unidad { get; set; }

    [DisplayName("Stock")]
    public decimal Stock { get; set; }

    // se compra por un lado y se gasta por otro: la harina se compra de a 25 kg
    // y se usa de a gramos. Van nulos mientras no se cargó la compra, y en lo
    // que no se compra se quedan nulos para siempre
    [DisplayName("Se compra de a")]
    public decimal? CantidadDeCompra { get; set; }

    [DisplayName("Precio")]
    public decimal? PrecioDeCompra { get; set; }

    [DisplayName("No se compra")]
    public bool Libre { get; set; }

    // en que recetas aparece: es por donde la lista de compras averigua cuanto
    // hace falta de cada cosa. Van separadas porque se cuentan distinto: la de
    // productos es por unidad y la de bases es por tanda
    public ICollection<ProductoIngrediente> UsosEnProductos { get; set; } = new List<ProductoIngrediente>();
    public ICollection<BaseIngrediente> UsosEnBases { get; set; } = new List<BaseIngrediente>();

    // el precio por gramo no se guarda: se divide. Guardarlo seria tener dos
    // numeros que pueden contradecirse el dia que cambie el precio
    [DisplayName("Precio por medida")]
    public decimal? PrecioPorMedida =>
        CantidadDeCompra > 0 && PrecioDeCompra.HasValue
            ? PrecioDeCompra.Value / CantidadDeCompra.Value
            : null;

    [DisplayName("Unidad")]
    public string Abreviatura => Unidad switch
    {
        Medida.Gramo => "g",
        Medida.Mililitro => "ml",
        _ => "u"
    };

    [DisplayName("Estado")]
    public string Estado => Libre ? "No se compra"
        : PrecioPorMedida.HasValue ? "Con precio"
        : "Falta cargar la compra";
}
