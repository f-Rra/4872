using System.Globalization;

namespace f4872.Helpers;

public static class Cultura
{
    public static CultureInfo Argentina()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-AR").Clone();

        // es-AR de fabrica escribe "$ 9.800,00" y el diseño muestra "$9.800": sin
        // espacio y sin centavos, porque ningun precio de la carta los tiene.
        // Donde si hacen falta -el costo por gramo- se pide N2 o C2 a mano
        cultura.NumberFormat.CurrencyPositivePattern = 0;
        cultura.NumberFormat.CurrencyNegativePattern = 1;
        cultura.NumberFormat.CurrencyDecimalDigits = 0;

        return cultura;
    }
}
