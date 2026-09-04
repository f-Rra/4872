using System.Diagnostics;
using f4872.Models;
using Microsoft.AspNetCore.Mvc;

namespace f4872.Controllers;

// solo la pantalla de error, a la que apunta UseExceptionHandler. La portada
// provisoria que tenía acá la reemplazó la carta
public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
