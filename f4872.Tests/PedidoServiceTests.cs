using System.Globalization;
using f4872.Data;
using f4872.Helpers;
using f4872.Models;
using f4872.ViewModels;
using Microsoft.EntityFrameworkCore;
using static f4872.Tests.DatosDePrueba;

namespace f4872.Tests;

// Confirmar es lo único que decide de verdad si un pedido entra: la pantalla se puede saltear, así
// que cada regla que apaga el botón se repite acá. Estos tests son esa lista de reglas.
public class PedidoServiceTests
{
    private static PedidoNuevo Pedido(Dictionary<string, int> items) => new()
    {
        Cliente = "Lucía Fernández",
        Telefono = "11 5555-0101",
        Direccion = "Av. Siempreviva 742, Palermo",
        Items = items
    };

    private static async Task<string> ErrorDe(Contexto contexto, PedidoNuevo datos)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearPedidos(contexto).Confirmar(datos));
        return error.Message;
    }

    private static Task<List<Pedido>> PedidosGuardados(Contexto contexto) =>
        contexto.Pedidos.Include(p => p.Items).ThenInclude(i => i.Quitados).ToListAsync();

    // ───────────── el camino feliz ─────────────

    [Fact]
    public async Task Confirmar_GuardaElPedidoComoNuevoYDevuelveSuNumero()
    {
        using var contexto = ConCarta();

        var id = await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1"] = 2, ["p4"] = 1 }));

        var pedido = Assert.Single(await PedidosGuardados(contexto));
        Assert.Equal(id, pedido.IdPedido);
        Assert.Equal(EstadoPedido.Nuevo, pedido.Estado);
        Assert.True(pedido.SinEntregar);
        Assert.Equal(2, pedido.Items.Count);
        // 2 margaritas a 9.800 y una focaccia a 6.800
        Assert.Equal(26400m, pedido.Total);
    }

    [Fact]
    public async Task Confirmar_LimpiaLosEspaciosDeLosDatosDelCliente()
    {
        using var contexto = ConCarta();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Cliente = "  Lucía Fernández  ";
        datos.Direccion = "  Av. Siempreviva 742  ";
        datos.Telefono = "  11 5555-0101  ";

        await CrearPedidos(contexto).Confirmar(datos);

        var pedido = Assert.Single(await PedidosGuardados(contexto));
        Assert.Equal("Lucía Fernández", pedido.Cliente);
        Assert.Equal("Av. Siempreviva 742", pedido.Direccion);
        Assert.Equal("11 5555-0101", pedido.Telefono);
    }

    [Fact]
    public async Task Confirmar_ElPrecioQuedaFijoEnElRenglonAunqueDespuesCambieLaCarta()
    {
        using var contexto = ConCarta();
        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1"] = 2 }));

        // el vendedor sube el precio de la margarita después de que el cliente pidió
        (await contexto.Productos.FindAsync(Margarita))!.Precio = 15000m;
        await contexto.SaveChangesAsync();

        var pedido = Assert.Single(await PedidosGuardados(contexto));
        Assert.Equal(9800m, pedido.Items.Single().PrecioUnitario);
        Assert.Equal(19600m, pedido.Total);
    }

    [Fact]
    public async Task Confirmar_UnPackSeCobraAlPrecioDelPackYGuardaCuantasUnidadesSon()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["e5x12"] = 1, ["e5x6"] = 2 }));

        var items = (await PedidosGuardados(contexto)).Single().Items.OrderBy(i => i.UnidadesPorPack).ToList();
        Assert.Equal(6, items[0].UnidadesPorPack);
        Assert.Equal(7900m, items[0].PrecioUnitario);
        Assert.Equal(15800m, items[0].Total);   // dos packs de 6
        Assert.Equal(12, items[1].UnidadesPorPack);
        Assert.Equal(15200m, items[1].PrecioUnitario);
    }

    [Fact]
    public async Task Confirmar_LaMismaPizzaConYSinUnIngredienteSonDosRenglones()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1"] = 1, ["p1|1"] = 1 }));

        var items = (await PedidosGuardados(contexto)).Single().Items;
        Assert.Equal(2, items.Count);
        Assert.Single(items, i => i.Quitados.Count == 0);
        Assert.Single(items, i => i.Quitados.Count == 1);
    }

    // ───────────── los datos del cliente ─────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public async Task Confirmar_SinNombre_Rechaza(string? nombre)
    {
        using var contexto = ConCarta();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Cliente = nombre;

        Assert.Equal("Falta tu nombre.", await ErrorDe(contexto, datos));
        Assert.Empty(await contexto.Pedidos.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Av 1")]        // cuatro caracteres
    [InlineData("      ")]
    public async Task Confirmar_SinDireccion_Rechaza(string? direccion)
    {
        using var contexto = ConCarta();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Direccion = direccion;

        Assert.Equal("Falta la dirección donde entregamos.", await ErrorDe(contexto, datos));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin números")]
    [InlineData("1234567")]         // siete dígitos
    [InlineData("(11) 55-55")]      // los signos no cuentan
    public async Task Confirmar_ConUnWhatsAppDeMenosDeOchoDigitos_Rechaza(string? telefono)
    {
        using var contexto = ConCarta();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Telefono = telefono;

        Assert.Equal("El WhatsApp tiene que tener al menos ocho dígitos.", await ErrorDe(contexto, datos));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("11 5555-0101")]
    [InlineData("+54 9 11 5555 0101")]
    public async Task Confirmar_AceptaUnWhatsAppEscritoDeCualquierManera(string telefono)
    {
        using var contexto = ConCarta();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Telefono = telefono;

        await CrearPedidos(contexto).Confirmar(datos);

        Assert.Single(await contexto.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task Confirmar_SinDatosDelClienteEnAbsoluto_RechazaConUnMensajeYNoExplota()
    {
        using var contexto = ConCarta();

        // un pedido armado a mano, sin ningún campo: tiene que ser un error del pedido, no un NullReference
        Assert.Equal("Falta tu nombre.", await ErrorDe(contexto, new PedidoNuevo()));
    }

    [Fact]
    public async Task Confirmar_UnPedidoVacio_Rechaza()
    {
        using var contexto = ConCarta();

        Assert.Equal("El pedido está vacío.", await ErrorDe(contexto, Pedido(new())));

        var sinItems = Pedido(new());
        sinItems.Items = null;
        Assert.Equal("El pedido está vacío.", await ErrorDe(contexto, sinItems));
    }

    // ───────────── la tienda y la carta ─────────────

    [Fact]
    public async Task Confirmar_ConLaTiendaCerrada_RechazaYNoGuardaNada()
    {
        using var contexto = ConCarta();
        (await contexto.Tienda.SingleAsync()).Abierta = false;
        await contexto.SaveChangesAsync();

        var error = await ErrorDe(contexto, Pedido(new() { ["p1"] = 1 }));

        Assert.StartsWith("Esta semana no tomamos pedidos.", error);
        Assert.Empty(await contexto.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task Confirmar_UnProductoAgotado_SeNombraParaQuePuedaSacarlo()
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { ["p1"] = 1, ["p3"] = 1 }));

        Assert.Equal("Se acabó: Calabresa. Sacalo del pedido y confirmá de nuevo.", error);
        // y no entra ni lo que sí había: es todo o nada
        Assert.Empty(await contexto.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task Confirmar_UnProductoQueYaNoEstaEnLaCarta_Rechaza()
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { ["p99"] = 1 }));

        Assert.Contains("ya no está en la carta", error);
    }

    // ───────────── los packs de empanadas ─────────────

    [Fact]
    public async Task Confirmar_UnPackDeAlgoQueNoEsEmpanada_Rechaza()
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { ["e1x6"] = 1 }));

        Assert.Equal("Margarita no se vende por pack.", error);
    }

    [Theory]
    [InlineData("e5x9")]     // un tamaño que no existe
    [InlineData("e5x24")]    // uno que existe pero está apagado
    public async Task Confirmar_UnPackQueNoEstaDisponible_Rechaza(string clave)
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { [clave] = 1 }));

        Assert.StartsWith("El pack de ", error);
        Assert.EndsWith(" ya no está.", error);
    }

    [Fact]
    public async Task Confirmar_UnaEmpanadaSuelta_Rechaza()
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { ["p5"] = 1 }));

        Assert.Equal("Carne suave se vende por pack, no por unidad.", error);
    }

    // ───────────── lo que se puede sacar ─────────────

    [Fact]
    public async Task Confirmar_SacarUnIngredienteQuitable_QuedaAnotadoEnElRenglon()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1|1"] = 1 }));

        var item = (await PedidosGuardados(contexto)).Single().Items.Single();
        Assert.Equal("sin Albahaca", item.Sin);
    }

    [Fact]
    public async Task Confirmar_SacarUnaSalsaQuitable_QuedaAnotadaPorSuNombre()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { [$"p1|s{SalsaDeTomate}"] = 1 }));

        var item = (await PedidosGuardados(contexto)).Single().Items.Single();
        Assert.Equal("sin Salsa de tomate", item.Sin);
    }

    [Fact]
    public async Task Confirmar_SacarVariasCosas_SeAnotanTodasEnOrden()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { [$"p1|1,s{SalsaDeTomate}"] = 1 }));

        var item = (await PedidosGuardados(contexto)).Single().Items.Single();
        Assert.Equal("sin Albahaca, Salsa de tomate", item.Sin);
    }

    [Fact]
    public async Task Confirmar_SacarDosVecesLoMismo_EsSacarloUnaVez()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1|1,1"] = 1 }));

        var item = (await PedidosGuardados(contexto)).Single().Items.Single();
        Assert.Single(item.Quitados);
    }

    [Fact]
    public async Task Confirmar_UnIngredienteYUnaSalsaConElMismoNombre_SonUnSoloSin()
    {
        using var contexto = ConCarta();

        // en la fugazzeta, el orégano es un ingrediente (3) y también una salsa (s3)
        await CrearPedidos(contexto).Confirmar(Pedido(new() { [$"p2|{Oregano},s{Oregano}"] = 1 }));

        var item = (await PedidosGuardados(contexto)).Single().Items.Single();
        Assert.Equal("sin Orégano", item.Sin);
        Assert.Single(item.Quitados);
    }

    [Theory]
    [InlineData("p1|2")]     // la muzzarella: está en la margarita pero no se puede sacar
    [InlineData("p1|99")]    // un ingrediente que la margarita no lleva
    [InlineData("p1|s99")]   // ni una salsa
    [InlineData("p2|1")]     // la albahaca existe, pero no en la fugazzeta
    public async Task Confirmar_SacarAlgoQueNoSePuedeSacar_Rechaza(string clave)
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { [clave] = 1 }));

        Assert.StartsWith("Cambió la receta de ", error);
        Assert.Empty(await contexto.Pedidos.ToListAsync());
    }

    // ───────────── las claves del carrito ─────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task Confirmar_UnaCantidadImposible_Rechaza(int cantidad)
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { ["p1"] = cantidad }));

        Assert.Equal($"El pedido trae una cantidad imposible: {cantidad}.", error);
    }

    [Fact]
    public async Task Confirmar_NoventaYNueveEsElTopeYSeAcepta()
    {
        using var contexto = ConCarta();

        await CrearPedidos(contexto).Confirmar(Pedido(new() { ["p1"] = 99 }));

        Assert.Equal(99 * 9800m, (await PedidosGuardados(contexto)).Single().Total);
    }

    [Theory]
    [InlineData("", "un renglón que no se entiende")]
    [InlineData("x1", "un renglón que no se entiende")]
    [InlineData("p", "un renglón que no se entiende")]
    [InlineData("p0", "un renglón que no se entiende")]
    [InlineData("pabc", "un renglón que no se entiende")]
    [InlineData("p1|2|3", "un renglón que no se entiende")]
    [InlineData("e5", "un pack que no se entiende")]
    [InlineData("e5x", "un pack que no se entiende")]
    [InlineData("ex6", "un pack que no se entiende")]
    [InlineData("e0x6", "un pack que no se entiende")]
    [InlineData("e5x0", "un pack que no se entiende")]
    [InlineData("p1|abc", "un ingrediente que no se entiende")]
    [InlineData("p1|0", "un ingrediente que no se entiende")]
    [InlineData("p1|07", "un ingrediente que no se entiende")]   // «07» tendría que ser «7»: no se acepta otro camino al mismo ingrediente
    [InlineData("p1|+1", "un ingrediente que no se entiende")]
    [InlineData("p1|s", "un ingrediente que no se entiende")]
    public async Task Confirmar_UnaClaveEscritaAMano_RechazaSinExplotar(string clave, string fragmento)
    {
        using var contexto = ConCarta();

        var error = await ErrorDe(contexto, Pedido(new() { [clave] = 1 }));

        Assert.Contains(fragmento, error);
        Assert.Empty(await contexto.Pedidos.ToListAsync());
    }

    // ───────────── el aviso por Telegram ─────────────

    [Fact]
    public async Task Confirmar_AvisaPorTelegramConElPedidoYQuienLoHizo()
    {
        CultureInfo.CurrentCulture = Cultura.Argentina();
        using var contexto = ConCarta();
        var telegram = new ManejadorQueGuarda();

        await CrearPedidos(contexto, telegram).Confirmar(Pedido(new() { ["p1|1"] = 2, ["e5x12"] = 1 }));

        var aviso = Assert.Single(telegram.Pedidos);
        Assert.Equal("https://api.telegram.org/botTOKEN-DE-PRUEBA/sendMessage", aviso.Direccion!.ToString());
        var texto = TextoEnviado(aviso.Cuerpo);
        Assert.Contains("<b>Pedido 0001</b>", texto);
        Assert.Contains("2× Margarita — sin Albahaca", texto);
        Assert.Contains("1× Carne suave · x12", texto);
        Assert.Contains("<b>Total $34.800</b>", texto);   // 2 × 9.800 + el pack de 12 a 15.200
        Assert.Contains("Lucía Fernández", texto);
        Assert.Contains("Av. Siempreviva 742, Palermo", texto);
        Assert.Contains("11 5555-0101", texto);
    }

    [Fact]
    public async Task Confirmar_ElAvisoEscapaLoQueEscribioElCliente()
    {
        CultureInfo.CurrentCulture = Cultura.Argentina();
        using var contexto = ConCarta();
        var telegram = new ManejadorQueGuarda();
        var datos = Pedido(new() { ["p1"] = 1 });
        datos.Cliente = "A < B & C";

        await CrearPedidos(contexto, telegram).Confirmar(datos);

        // sin escapar, el «<» rompería el mensaje y Telegram no lo mandaría
        Assert.Contains("A &lt; B &amp; C", TextoEnviado(telegram.Pedidos.Single().Cuerpo));
    }

    [Fact]
    public async Task Confirmar_ElAvisoSoloSaleDespuesDeGuardarElPedido()
    {
        using var contexto = ConCarta();
        var telegram = new ManejadorQueGuarda();

        // un pedido que se rechaza no avisa nada: no hay nada que avisar
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearPedidos(contexto, telegram).Confirmar(Pedido(new() { ["p3"] = 1 })));

        Assert.Empty(telegram.Pedidos);
    }

    [Theory]
    [InlineData(true)]    // Telegram contesta con un error
    [InlineData(false)]   // se corta la conexión
    public async Task Confirmar_SiTelegramFalla_ElPedidoEntraIgual(bool contestaConError)
    {
        using var contexto = ConCarta();
        var telegram = new ManejadorQueGuarda();
        if (contestaConError)
        {
            telegram.Codigo = System.Net.HttpStatusCode.InternalServerError;
        }
        else
        {
            telegram.Falla = new HttpRequestException("sin red");
        }

        var id = await CrearPedidos(contexto, telegram).Confirmar(Pedido(new() { ["p1"] = 1 }));

        Assert.Equal(id, Assert.Single(await contexto.Pedidos.ToListAsync()).IdPedido);
    }

    [Fact]
    public async Task Confirmar_SinTelegramConfigurado_ElPedidoEntraYNoSaleNingunAviso()
    {
        using var contexto = ConCarta();
        var telegram = new ManejadorQueGuarda();

        await CrearPedidos(contexto, telegram, telegramConfigurado: false).Confirmar(Pedido(new() { ["p1"] = 1 }));

        Assert.Single(await contexto.Pedidos.ToListAsync());
        Assert.Empty(telegram.Pedidos);
    }
}
