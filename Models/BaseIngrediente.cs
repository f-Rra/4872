using System.ComponentModel;

namespace f4872.Models;

public class BaseIngrediente
{
    public int IdBase { get; set; }
    public Base Base { get; set; } = null!;

    public int IdIngrediente { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    // ojo: esta cantidad es de la TANDA ENTERA, no de una unidad. La receta del
    // bollo son 1 kg de harina para seis bollos, y asi se carga, porque asi la
    // dice el. Dividir por el rinde es problema del calculo, no de la tabla
    [DisplayName("Cantidad de la tanda")]
    public decimal Cantidad { get; set; }

    // lo que le toca a un bollo. Necesita la base cargada: consultado sin
    // Include da nulo en vez de un numero equivocado
    [DisplayName("Por unidad")]
    public decimal? PorUnidad => Base is null ? null : Cantidad / Base.Rinde;
}
