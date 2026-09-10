using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace f4872.Controllers;

// El panel lo usa una persona, así que no hay usuarios ni roles: hay una clave
// y una cookie. Sin ASP.NET Identity, que traería tablas de usuarios, de roles
// y de reclamos para un solo vendedor que no va a tener compañeros.
[Authorize]
[Route("panel")]
public class PanelController : Controller
{
    private readonly string? _clave;

    // Un segundo de espera cuando la clave está mal. No molesta al que se
    // equivoca una vez y le arruina el día al que quiere probar de a miles.
    private static readonly TimeSpan Castigo = TimeSpan.FromSeconds(1);

    public PanelController(IConfiguration configuracion)
    {
        _clave = configuracion["Panel:Clave"];
    }

    [Authorize]
    public IActionResult Index()
    {
        return View();
    }

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
