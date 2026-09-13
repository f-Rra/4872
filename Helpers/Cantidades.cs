using System.Globalization;
using System.Text.RegularExpressions;
using f4872.Models;

namespace f4872.Helpers;

public static class Cantidades
{
    // Los ingredientes se guardan siempre en la unidad chica —gramos y
    // mililitros— porque así se usan en la receta. Pero una lista de compras que
    // diga «10.400 g de harina» se lee mal: eso son diez kilos y medio, y es lo
    // que uno pide en el almacén. De mil para arriba se sube de unidad.
    //
    // `exacto` es para el campo que se edita: ahi el numero tiene que poder
    // volver a leerse tal cual, y redondear a dos decimales le comeria gramos al
    // stock cada vez que se guarda un renglon sin haberlo tocado.
    public static string Bonito(decimal cantidad, Medida unidad, bool exacto = false)
    {
        if (cantidad >= 1000 && unidad is Medida.Gramo or Medida.Mililitro)
        {
            return Redondo(cantidad / 1000, exacto ? 5 : 2) + (unidad == Medida.Gramo ? " kg" : " l");
        }

        return Redondo(cantidad, exacto ? 3 : 1) + " " + Abreviatura(unidad);
    }

    // La vuelta de Bonito: el campo de stock dice «11 kg» y hay que poder leerlo
    // de nuevo. Sin unidad escrita el numero se toma en la unidad chica, asi que
    // tipear «11500» encima de «11 kg» tambien anda.
    //
    // Devuelve nulo -y no cero- cuando no se entiende: dejar el campo vacio o
    // escribir cualquier cosa tiene que dejar el stock como estaba, no borrarlo.
    public static decimal? Leer(string? texto, Medida unidad)
    {
        var limpio = (texto ?? "").Trim().ToLowerInvariant();

        // un stock negativo no existe, y limpiar el signo lo convertiria en su
        // opuesto sin avisar: mejor no entender nada
        if (limpio.Length == 0 || limpio.Contains('-'))
        {
            return null;
        }

        // el punto separa los miles y la coma los decimales: «11.500,5»
        var numero = new string(limpio.Replace(".", "")
            .Where(c => char.IsDigit(c) || c == ',').ToArray())
            .Replace(',', '.');

        if (!decimal.TryParse(numero, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
        {
            return null;
        }

        // «2 kg» son 2000 g y «1,4 l» son 1400 ml. La l de «ml» no cuenta
        var enGrande = unidad switch
        {
            Medida.Gramo => limpio.Contains("kg"),
            Medida.Mililitro => Regex.IsMatch(limpio, "(^|[^m])l"),
            _ => false
        };

        return enGrande ? valor * 1000 : valor;
    }

    public static string Abreviatura(Medida unidad) => unidad switch
    {
        Medida.Gramo => "g",
        Medida.Mililitro => "ml",
        _ => "u"
    };

    // sin ceros al final: 2,50 kg se lee peor que 2,5 kg, y 2,00 peor que 2
    private static string Redondo(decimal valor, int decimales) =>
        Math.Round(valor, decimales).ToString("0." + new string('#', decimales));
}
