using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using f4872.Data;
using f4872.Helpers;
using f4872.Models;
using f4872.ViewModels;

namespace f4872.Controllers;

// El panel lo usa una persona, así que no hay usuarios ni roles: hay una clave
// y una cookie. Sin ASP.NET Identity, que traería tablas de usuarios, de roles
// y de reclamos para un solo vendedor que no va a tener compañeros.
[Authorize]
[Route("panel")]
public class PanelController : Controller
{
    private readonly Contexto _contexto;
    private readonly string? _clave;

    // Un segundo de espera cuando la clave está mal. No molesta al que se
    // equivoca una vez y le arruina el día al que quiere probar de a miles.
    private static readonly TimeSpan Castigo = TimeSpan.FromSeconds(1);

    public PanelController(Contexto contexto, IConfiguration configuracion)
    {
        _contexto = contexto;
        _clave = configuracion["Panel:Clave"];
    }

    public async Task<IActionResult> Index()
    {
        var marco = await Marco();

        return View(new InicioVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Cifras = await Cifras()
        });
    }

    // Las cinco tarjetas del tablero.
    //
    // La maqueta trae un catalogo de veintiseis cifras y deja elegir cinco desde
    // el titulo de cada tarjeta. Estas cinco salen de ese catalogo y son las que
    // se pueden calcular hoy: las otras tres que trae de fabrica -Falta comprar,
    // Produccion y Costo- necesitan las recetas y el stock, que son las pantallas
    // que faltan. Cuando existan, entra el selector y vuelven las de la maqueta.
    private async Task<IReadOnlyList<Cifra>> Cifras()
    {
        // un solo viaje: todo lo que sigue sale de los pedidos sin entregar, y
        // son pocos por definicion -los entregados no cuentan-
        var abiertos = await _contexto.Pedidos
            .Where(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando)
            .Select(x => new
            {
                x.IdPedido,
                x.Telefono,
                x.FechaPedido,
                Nuevo = x.Estado == EstadoPedido.Nuevo,
                Plata = x.Items.Sum(i => i.Cantidad * i.PrecioUnitario)
            })
            .ToListAsync();

        var viejo = abiertos.OrderBy(x => x.FechaPedido).FirstOrDefault();

        return
        [
            new Cifra
            {
                Titulo = "Pedidos nuevos",
                Valor = abiertos.Count(x => x.Nuevo).ToString(),
                Nota = "sin abrir"
            },
            new Cifra
            {
                Titulo = "Sin entregar",
                Valor = abiertos.Count.ToString(),
                Nota = "abiertos en total"
            },
            new Cifra
            {
                Titulo = "El más viejo",
                Valor = viejo is null ? "—" : $"{Reloj.HorasDesde(viejo.FechaPedido)} h",
                Nota = viejo is null
                    ? "no hay pedidos abiertos"
                    : $"el {viejo.IdPedido:0000}, de las {Reloj.EnBuenosAires(viejo.FechaPedido):HH:mm}"
            },
            new Cifra
            {
                Titulo = "Clientes",
                // por telefono y no por nombre: dos Juan son dos personas, y el
                // mismo telefono pidiendo dos veces es una sola esperando
                Valor = abiertos.Select(x => x.Telefono).Distinct().Count().ToString(),
                Nota = "esperando"
            },
            new Cifra
            {
                Titulo = "Total",
                Valor = abiertos.Sum(x => x.Plata).ToString("C"),
                Nota = "sin entregar todavía"
            }
        ];
    }

    // El interruptor. Por POST porque cambia algo, y volviendo a Inicio para que
    // recargar la pagina no lo vuelva a tocar.
    [HttpPost("llave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Llave()
    {
        var tienda = await _contexto.Tienda.SingleOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "Falta la fila de la tienda. Corre dotnet ef database update.");

        tienda.Abierta = !tienda.Abierta;
        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // Lo que necesita el marco, que se dibuja en todas las pantallas del panel
    private async Task<PanelVm> Marco() => new()
    {
        Abierta = await _contexto.Tienda.AnyAsync(x => x.Abierta),
        SinEntregar = await _contexto.Pedidos
            .CountAsync(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando)
    };

    [AllowAnonymous]
    [HttpGet("entrar")]
    public IActionResult Entrar(string? volverA = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        // Sin clave configurada el panel queda cerrado, no abierto. Es la única
        // manera de equivocarse que no se puede permitir acá.
        ViewData["SinClave"] = string.IsNullOrWhiteSpace(_clave);
        ViewData["VolverA"] = Seguro(volverA);
        return View();
    }

    [AllowAnonymous]
    [HttpPost("entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(string? clave, string? volverA)
    {
        if (string.IsNullOrWhiteSpace(_clave))
        {
            ViewData["SinClave"] = true;
            return View();
        }

        if (!Coincide(clave ?? ""))
        {
            await Task.Delay(Castigo);
            // no dice si la clave era corta, larga o parecida: solo que no es
            ViewData["Error"] = "Esa no es la clave.";
            ViewData["VolverA"] = Seguro(volverA);
            return View();
        }

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "vendedor")],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // persistente: es su computadora y su teléfono, y tener que escribir la
        // clave cada vez que abre el panel un sábado a la mañana no protege nada
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect(Seguro(volverA) ?? Url.Action(nameof(Index))!);
    }

    // por POST y no por un enlace: un GET que cierra la sesión lo dispara
    // cualquier página ajena con una etiqueta de imagen apuntada acá
    [HttpPost("salir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    // La comparación es de tiempo fijo: una común corta apenas encuentra la
    // primera letra distinta, y esa diferencia de microsegundos, medida muchas
    // veces, deja adivinar la clave letra por letra.
    private bool Coincide(string tecleada) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(tecleada),
            Encoding.UTF8.GetBytes(_clave!));

    // La dirección a la que volver viene de la cookie, que la arma el middleware,
    // pero llega por la query y cualquiera la puede escribir. Si no es de este
    // sitio no se usa: si no, esto es un trampolín para mandar a otro lado.
    private string? Seguro(string? destino) =>
        !string.IsNullOrWhiteSpace(destino) && Url.IsLocalUrl(destino) ? destino : null;
}
