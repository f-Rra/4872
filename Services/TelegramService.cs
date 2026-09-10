using System.Net.Http.Json;

namespace f4872.Services;

// El aviso de que entró un pedido.
//
// Telegram y no un mail porque el aviso tiene que sonar en el teléfono a las
// once de la noche, y porque un bot se configura en dos minutos y no cuesta
// nada. Después él coordina por WhatsApp, que es otra conversación.
//
// Nada de lo que pasa acá adentro puede voltear un pedido: el pedido ya está
// guardado cuando esto corre. Si Telegram no contesta, el pedido existe igual y
// lo va a ver en el panel; al revés —perder el pedido porque no salió el
// aviso— sería mucho peor.
public class TelegramService
{
    private readonly IHttpClientFactory _fabrica;
    private readonly ILogger<TelegramService> _registro;
    private readonly string? _token;
    private readonly string? _chat;

    public TelegramService(IHttpClientFactory fabrica, IConfiguration configuracion, ILogger<TelegramService> registro)
    {
        _fabrica = fabrica;
        _registro = registro;
        _token = configuracion["Telegram:Token"];
        _chat = configuracion["Telegram:Chat"];
    }

    // Sin token la tienda funciona igual: se pueden hacer pedidos y quedan
    // guardados. Es lo que pasa en la máquina de uno mientras se programa.
    public bool Configurado => !string.IsNullOrWhiteSpace(_token) && !string.IsNullOrWhiteSpace(_chat);

    public async Task Avisar(string texto)
    {
        if (!Configurado)
        {
            _registro.LogInformation("Sin Telegram configurado, el aviso no sale. Falta Telegram:Token o Telegram:Chat.");
            return;
        }

        try
        {
            var cliente = _fabrica.CreateClient(nameof(TelegramService));
            using var respuesta = await cliente.PostAsJsonAsync(
                $"https://api.telegram.org/bot{_token}/sendMessage",
                new
                {
                    chat_id = _chat,
                    text = texto,
                    parse_mode = "HTML",
                    disable_web_page_preview = true
                });

            if (!respuesta.IsSuccessStatusCode)
            {
                // el cuerpo dice cuál de las dos claves está mal, que es lo
                // único que se puede llegar a arreglar desde acá
                _registro.LogError("Telegram contestó {Codigo}: {Cuerpo}",
                    (int)respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
            }
        }
        catch (Exception e)
        {
            _registro.LogError(e, "No salió el aviso de Telegram.");
        }
    }

    // El mensaje va con parse_mode HTML para poder resaltar el número y el
    // total. Eso obliga a escapar lo que escribió una persona: un cliente que
    // se llame «A < B» rompería el mensaje entero y Telegram no lo mandaría.
    public static string Escapar(string texto) => texto
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
