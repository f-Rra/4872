using System.Diagnostics;
using f4872.Data;
using f4872.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace f4872.Controllers;

public class HomeController : Controller
{
    private readonly Contexto _contexto;

    public HomeController(Contexto contexto)
    {
        _contexto = contexto;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Base"] = await EstadoDeLaBase();
        return View();
    }

    // provisorio: es lo unico que hace verificable el commit que conecta la base,
    // y se va con esta pantalla cuando la entrada del sitio pase a ser la carta
    private async Task<string> EstadoDeLaBase()
    {
        try
        {
            await _contexto.Database.OpenConnectionAsync();
            await _contexto.Database.CloseConnectionAsync();
            return "conectada";
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            // 3D000 es "la base no existe": el servidor contesto y la clave estaba
            // bien, solo falta crearla. Es lo esperado hasta la primera migracion
            return "el servidor contesta, pero la base f4872 todavía no existe";
        }
        catch (Exception ex)
        {
            return "no responde — " + ex.Message;
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
