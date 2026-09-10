using f4872.Models;

namespace f4872.Helpers;

public static class Cantidades
{
    // Los ingredientes se guardan siempre en la unidad chica —gramos y
    // mililitros— porque así se usan en la receta. Pero una lista de compras que
    // diga «10.400 g de harina» se lee mal: eso son diez kilos y medio, y es lo
    // que uno pide en el almacén. De mil para arriba se sube de unidad.
    public static string Bonito(decimal cantidad, Medida unidad)
    {
        if (cantidad >= 1000 && unidad is Medida.Gramo or Medida.Mililitro)
        {
            return Redondo(cantidad / 1000, 2) + (unidad == Medida.Gramo ? " kg" : " l");
        }

        return Redondo(cantidad, 1) + " " + Abreviatura(unidad);
    }

    public static string Abreviatura(Medida unidad) => unidad switch
    {
        Medida.Gramo => "g",
        Medida.Mililitro => "ml",
        _ => "u"
    };

    // sin ceros al final: 2,50 kg se lee peor que 2,5 kg, y 2,00 peor que 2
    private static string Redondo(decimal valor, int decimales) =>
        Math.Round(valor, decimales).ToString("0.##");
}
