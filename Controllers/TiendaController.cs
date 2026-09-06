using Microsoft.AspNetCore.Mvc;

namespace f4872.Controllers;

public class TiendaController : Controller
{
    // el inicio: existe para el que llega de cero, porque el nombre no dice que
    // esto es una pizzería ni que el pedido tarda días
    public IActionResult Index()
    {
        return View();
    }

    // la carta. La acción se llama en español como todo el código; /shop es
    // solo la dirección que ve el cliente
    [Route("shop")]
    public IActionResult Carta()
    {
        return View();
    }
}
