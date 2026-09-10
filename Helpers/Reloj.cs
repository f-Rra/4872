namespace f4872.Helpers;

// Las fechas se guardan en UTC, que es lo que devuelve now() en Postgres, y la
// conversión a hora de acá es solo para mostrar. Este es el único lugar que la
// hace, así que si mañana el servidor queda en otro huso, no cambia nada.
public static class Reloj
{
    // .NET acepta el nombre IANA en Windows y en Linux desde que usa ICU, pero
    // no en todas las instalaciones: si no lo encuentra, prueba el de Windows
    private static readonly TimeZoneInfo Zona = Buscar();

    public static DateTime EnBuenosAires(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    // Cuánto hace que entró, en horas enteras. Para el panel no sirve el minuto:
    // la pregunta es si un pedido lleva dos horas o dos días esperando.
    public static int HorasDesde(DateTime utc) =>
        (int)(DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc)).TotalHours;

    private static TimeZoneInfo Buscar()
    {
        foreach (var nombre in new[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(nombre); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Argentina no tiene horario de verano desde 2009, así que un desfase
        // fijo de tres horas es correcto y no una aproximación
        return TimeZoneInfo.CreateCustomTimeZone("4872-arg", TimeSpan.FromHours(-3), "Argentina", "Argentina");
    }
}
