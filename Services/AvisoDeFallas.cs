using System.Diagnostics;
using f4872.Helpers;
using Microsoft.AspNetCore.Diagnostics;

namespace f4872.Services;

// El aviso de que algo se rompió en producción.
//
// Si el checkout falla, el cliente ve «Algo salió mal» y el único que se entera
// es él, cuando alguien le escribe. Esto le manda un mensaje en el momento, por
// el mismo bot y al mismo chat de los pedidos.
//
// Se engancha al manejador de errores de ASP.NET y no lo reemplaza: devuelve
// false siempre, así que la pantalla de error sale igual que antes. La
// excepción entera, con su traza, ya queda en el registro de Railway; el aviso
// solo dice dónde, cuándo y trae la referencia que ve el cliente en pantalla,
// para encontrarla ahí.
//
// Lo que NO lleva, a propósito: el texto de la excepción, la traza ni la
// dirección con lo que traiga detrás. Un error de la base puede nombrar el
// valor que no entró —un nombre, un teléfono— y esos datos no tienen que viajar
// por acá. Tampoco avisa de lo que no es una falla de la tienda: una petición
// mal armada o un cliente que cortó la conexión.
//
// Es un singleton porque el tope vive en memoria y tiene que ser uno solo para
// todos los pedidos. El servicio de Telegram, que es por pedido, se lo pide al
// pedido que falló.
public class AvisoDeFallas : IExceptionHandler
{
    // Cuánto se espera entre un aviso y el siguiente, pase lo que pase. Si se
    // cae la base falla cada pantalla que se abre, y sin esto serían cientos de
    // mensajes en el chat donde también llegan los pedidos. Lo que pasa
    // mientras tanto no se pierde: se cuenta y sale en el próximo aviso.
    private static readonly TimeSpan Entre = TimeSpan.FromMinutes(10);

    private readonly ILogger<AvisoDeFallas> _registro;
    private readonly object _candado = new();

    // las fallas que no avisaron desde el último aviso, y cuántas veces cada una
    private readonly Dictionary<string, int> _calladas = new();
    private DateTime _ultimoAviso = DateTime.MinValue;

    public AvisoDeFallas(ILogger<AvisoDeFallas> registro)
    {
        _registro = registro;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken corte)
    {
        try
        {
            if (excepcion is BadHttpRequestException || contexto.RequestAborted.IsCancellationRequested)
            {
                return false;
            }

            var donde = $"{contexto.Request.Method} {Donde(contexto)}";
            var tipo = excepcion.GetType().Name;

            string? calladas;
            lock (_candado)
            {
                var ahora = DateTime.UtcNow;
                if (ahora - _ultimoAviso < Entre)
                {
                    var clave = $"{donde} · {tipo}";
                    _calladas[clave] = _calladas.GetValueOrDefault(clave) + 1;
                    return false;
                }

                // se anota antes de mandar: si Telegram tarda, las fallas que
                // llegan mientras tanto no se cuelan a mandar cada una el suyo
                _ultimoAviso = ahora;
                calladas = Resumen(_calladas);
                _calladas.Clear();
            }

            // la misma que muestra la pantalla de error (HomeController)
            var referencia = Activity.Current?.Id ?? contexto.TraceIdentifier;

            await contexto.RequestServices.GetRequiredService<TelegramService>()
                .Avisar(Mensaje(donde, tipo, referencia, calladas));
        }
        catch (Exception e)
        {
            // Nada de lo que pasa acá puede romper la pantalla de error: si este
            // código falla, el cliente igual tiene que ver «Algo salió mal» y no
            // una página en blanco.
            _registro.LogError(e, "No salió el aviso de la falla.");
        }

        return false;
    }

    // De dónde salió, con el nombre de la acción y no con la dirección: es
    // corto, junta las veces que falla lo mismo (/gracias/41 y /gracias/42 son
    // una sola) y no puede traer nada que haya escrito quien mandó la petición.
    //
    // La ruta se lee de acá y no de Request: el manejador de errores la vacía
    // antes de llamar, para volver a correr la tubería con la pantalla de error.
    private static string Donde(HttpContext contexto)
    {
        var valores = contexto.Features.Get<IExceptionHandlerPathFeature>()?.RouteValues;
        var controlador = valores?["controller"]?.ToString();
        var accion = valores?["action"]?.ToString();

        return controlador is null || accion is null ? "(fuera de las pantallas)" : $"{controlador}/{accion}";
    }

    // Lo más repetido primero y no más de tres renglones: es para saber si falla
    // una cosa o varias, no para leer un registro.
    private static string? Resumen(Dictionary<string, int> calladas)
    {
        if (calladas.Count == 0)
        {
            return null;
        }

        var renglones = calladas
            .OrderByDescending(x => x.Value)
            .Take(3)
            .Select(x => $"{x.Value} × {x.Key}")
            .ToList();

        var resto = calladas.Count - 3;
        if (resto > 0)
        {
            renglones.Add(resto == 1 ? "y 1 falla distinta más" : $"y {resto} fallas distintas más");
        }

        return string.Join("\n", renglones);
    }

    // Va con parse_mode HTML, igual que el de los pedidos, así que lo que se
    // escribe pasa por Escapar aunque hoy salga de nuestro propio código.
    private static string Mensaje(string donde, string tipo, string referencia, string? calladas)
    {
        var hora = Reloj.EnBuenosAires(DateTime.UtcNow);

        var texto = "<b>Falló algo en 48·72</b>\n\n" +
                    $"{TelegramService.Escapar(donde)}\n" +
                    $"{TelegramService.Escapar(tipo)} · {hora:HH:mm}\n\n" +
                    $"<code>{TelegramService.Escapar(referencia)}</code>";

        if (calladas is not null)
        {
            texto += "\n\nDesde el último aviso hubo más:\n" + TelegramService.Escapar(calladas);
        }

        return texto;
    }
}
