using Microsoft.AspNetCore.Mvc;

namespace f4872.Controllers;

public class TiendaController : Controller
{
    // la carta es la puerta del sitio: no hay pantalla de inicio, se entra
    // directo acá. Todavía sin datos, eso llega en el commit 15
    public IActionResult Index()
    {
        return View();
    }
}
