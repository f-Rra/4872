using System.Net;
using System.Text.Json;
using f4872.Data;
using f4872.Models;
using f4872.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace f4872.Tests;

// Lo que comparten los tests: una base en memoria con una carta chica y un TelegramService que
// no sale a internet. Cada test arma la suya, así que lo que uno guarda no lo ve el siguiente.
public static class DatosDePrueba
{
    // Los productos. Tres pizzas (una agotada), una focaccia y un gusto de empanada, que no
    // tiene precio propio: se cobra por pack.
    public const int Margarita = 1;
    public const int Fugazzeta = 2;
    public const int Calabresa = 3;   // agotada
    public const int Romero = 4;      // focaccia
    public const int CarneSuave = 5;  // empanada

    // Los ingredientes de la Margarita: la albahaca se puede sacar y la muzzarella no
    public const int Albahaca = 1;
    public const int Muzzarella = 2;

    // El 3 es a propósito el número de un ingrediente Y de una receta, los dos llamados Orégano:
    // el pedido los distingue con una «s» («3» es el ingrediente, «s3» la salsa)
    public const int Oregano = 3;

    public const int SalsaDeTomate = 7;

    public static Contexto CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<Contexto>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var contexto = new Contexto(opciones);

        // lo que siembra el propio modelo: la tienda, abierta
        contexto.Database.EnsureCreated();
        return contexto;
    }

    public static Contexto ConCarta()
    {
        var contexto = CrearContexto();

        contexto.Productos.AddRange(
            new Producto { IdProducto = Margarita, Familia = Familia.Pizza, Nombre = "Margarita", Precio = 9800m, Posicion = 1 },
            new Producto { IdProducto = Fugazzeta, Familia = Familia.Pizza, Nombre = "Fugazzeta", Precio = 11200m, Posicion = 2 },
            new Producto { IdProducto = Calabresa, Familia = Familia.Pizza, Nombre = "Calabresa", Precio = 12400m, Posicion = 3, Activo = false },
            new Producto { IdProducto = Romero, Familia = Familia.Focaccia, Nombre = "Romero y sal", Precio = 6800m, Posicion = 1 },
            new Producto { IdProducto = CarneSuave, Familia = Familia.Empanada, Nombre = "Carne suave", Precio = null, Posicion = 1 });

        contexto.Packs.AddRange(
            new Pack { Unidades = 6, Precio = 7900m },
            new Pack { Unidades = 12, Precio = 15200m },
            new Pack { Unidades = 24, Precio = 29000m, Activo = false });

        contexto.Ingredientes.AddRange(
            new Ingrediente { IdIngrediente = Albahaca, Nombre = "Albahaca", Unidad = Medida.Unidad },
            new Ingrediente { IdIngrediente = Muzzarella, Nombre = "Muzzarella", Unidad = Medida.Gramo },
            new Ingrediente { IdIngrediente = Oregano, Nombre = "Orégano", Unidad = Medida.Gramo });

        contexto.Recetas.AddRange(
            new Receta { IdReceta = SalsaDeTomate, Nombre = "Salsa de tomate", Tipo = TipoReceta.Salsa },
            new Receta { IdReceta = Oregano, Nombre = "Orégano", Tipo = TipoReceta.Salsa });

        contexto.ProductoIngredientes.AddRange(
            new ProductoIngrediente { IdProducto = Margarita, IdIngrediente = Albahaca, Cantidad = 4, Quitable = true },
            new ProductoIngrediente { IdProducto = Margarita, IdIngrediente = Muzzarella, Cantidad = 120, Quitable = false },
            new ProductoIngrediente { IdProducto = Fugazzeta, IdIngrediente = Oregano, Cantidad = 2, Quitable = true });

        contexto.ProductoSalsas.AddRange(
            new ProductoSalsa { IdProducto = Margarita, IdReceta = SalsaDeTomate, Quitable = true },
            new ProductoSalsa { IdProducto = Fugazzeta, IdReceta = Oregano, Quitable = true });

        contexto.SaveChanges();
        return contexto;
    }

    // ---------- Telegram ----------

    // Un TelegramService de verdad, pero cuyo «internet» es el manejador que se le pase: guarda lo
    // que se le pidió mandar y contesta lo que el test quiera.
    public static TelegramService CrearTelegram(ManejadorQueGuarda manejador, bool configurado = true)
    {
        var valores = configurado
            ? new Dictionary<string, string?> { ["Telegram:Token"] = "TOKEN-DE-PRUEBA", ["Telegram:Chat"] = "12345" }
            : new Dictionary<string, string?>();

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return new TelegramService(new FabricaDeClientes(manejador), configuracion, NullLogger<TelegramService>.Instance);
    }

    public static PedidoService CrearPedidos(Contexto contexto, ManejadorQueGuarda? manejador = null, bool telegramConfigurado = true) =>
        new(contexto, CrearTelegram(manejador ?? new ManejadorQueGuarda(), telegramConfigurado));

    // El texto de un aviso, tal como lo vería Telegram. Va dentro de un JSON que escapa los
    // acentos y los signos («×» sale como «×»), así que se lee de ahí y no del cuerpo crudo.
    public static string TextoEnviado(string cuerpoJson) =>
        JsonDocument.Parse(cuerpoJson).RootElement.GetProperty("text").GetString()!;
}

public sealed class ManejadorQueGuarda : HttpMessageHandler
{
    public List<(Uri? Direccion, string Cuerpo)> Pedidos { get; } = new();

    public HttpStatusCode Codigo { get; set; } = HttpStatusCode.OK;

    // si no es nulo, es lo que «se cortó» en el camino
    public Exception? Falla { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelar)
    {
        var cuerpo = solicitud.Content is null ? "" : await solicitud.Content.ReadAsStringAsync(cancelar);
        Pedidos.Add((solicitud.RequestUri, cuerpo));

        if (Falla is not null)
        {
            throw Falla;
        }

        return new HttpResponseMessage(Codigo);
    }
}

internal sealed class FabricaDeClientes : IHttpClientFactory
{
    private readonly HttpMessageHandler _manejador;

    public FabricaDeClientes(HttpMessageHandler manejador) => _manejador = manejador;

    public HttpClient CreateClient(string nombre) => new(_manejador, disposeHandler: false);
}
