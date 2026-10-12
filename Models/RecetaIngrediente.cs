namespace f4872.Models;

public class RecetaIngrediente
{
    public int IdReceta { get; set; }
    public Receta Receta { get; set; } = null!;

    public int IdIngrediente { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    // ojo: esta cantidad es de la RECETA ENTERA, no de una pieza. La del bollo
    // son 1 kg de harina para seis bollos, y asi se carga, porque asi la dice
    // el. Dividir por el rinde es problema del calculo, no de la tabla
    public decimal Cantidad { get; set; }
}
