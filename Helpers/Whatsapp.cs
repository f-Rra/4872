namespace f4872.Helpers;

// El enlace para escribirle al cliente desde el panel.
//
// wa.me pide el número entero con código de país y sin nada más. El cliente lo
// escribe como quiere —«11 5555 5555», «011 15 5555-5555», «+54 9 11 5555
// 5555»— así que hay que llevarlo a una sola forma.
//
// Cuando no se puede, devuelve nulo y la pantalla no muestra el enlace. Es a
// propósito: un wa.me mal armado no falla, abre la conversación de otra persona,
// y escribirle a un desconocido sobre un pedido es peor que no tener el botón.
public static class Whatsapp
{
    private const string Argentina = "54";

    // El 9 que va después del 54 en los celulares. Argentina es el único país
    // que lo pide, y sin él wa.me abre un chat que no existe.
    private const string Celular = "9";

    // Un número nacional es código de área más abonado, y siempre suma diez:
    // 11 + ocho en Buenos Aires, 351 + siete en Córdoba. Lo que no dé diez no
    // se sabe leer, y no se inventa.
    private const int Nacional = 10;

    public static string? Enlace(string? telefono)
    {
        var digitos = new string((telefono ?? "").Where(char.IsDigit).ToArray());

        // el 0 de larga distancia no va nunca en un número internacional
        digitos = digitos.TrimStart('0');

        if (digitos.StartsWith(Argentina + Celular))
        {
            digitos = digitos[(Argentina.Length + Celular.Length)..];
        }
        else if (digitos.StartsWith(Argentina) && digitos.Length > Nacional)
        {
            digitos = digitos[Argentina.Length..];
        }

        digitos = SinElQuince(digitos);

        return digitos.Length == Nacional ? $"https://wa.me/{Argentina}{Celular}{digitos}" : null;
    }

    // El 15 va entre el área y el abonado cuando se llama desde un fijo, y no
    // va nunca en el formato internacional. Se saca solo si al sacarlo el
    // número queda de diez: así un abonado que de casualidad empiece con 15
    // —«11 1555-4433»— no se rompe.
    private static string SinElQuince(string digitos)
    {
        if (digitos.Length <= Nacional)
        {
            return digitos;
        }

        // el área es de dos, tres o cuatro dígitos según la ciudad
        for (var area = 2; area <= 4; area++)
        {
            if (digitos.Length > area + 2 &&
                digitos[area] == '1' && digitos[area + 1] == '5' &&
                digitos.Length - 2 == Nacional)
            {
                return digitos[..area] + digitos[(area + 2)..];
            }
        }

        return digitos;
    }
}
