using f4872.Data;
using f4872.Models;
using f4872.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace f4872.Controllers;

public class TiendaController : Controller
{
    private readonly Contexto _contexto;

    public TiendaController(Contexto contexto)
    {
        _contexto = contexto;
    }

    // el inicio: existe para el que llega de cero, porque el nombre no dice que
    // esto es una pizzería ni que el pedido tarda días
    public IActionResult Index()
    {
        return View();
    }

    // la carta. La acción se llama en español como todo el código; /shop es
    // solo la dirección que ve el cliente
    [Route("shop")]
    public async Task<IActionResult> Carta()
    {
        // una sola consulta para las dos familias: son la misma forma de renglón
        // y traerlas por separado serían dos viajes a la base para nada
        var renglones = await _contexto.Productos
            .Where(x => x.Familia == Familia.Pizza || x.Familia == Familia.Focaccia)
            .OrderBy(x => x.IdProducto)
            .Select(x => new
            {
                x.Familia,
                Renglon = new RenglonCarta
                {
                    IdProducto = x.IdProducto,
                    Nombre = x.Nombre,
                    Precio = x.Precio,
                    Activo = x.Activo,
                    // sin columna de orden, el de la receta no está garantizado:
                    // ver la nota del commit. Por IdIngrediente al menos es estable
                    Ingredientes = x.Receta
                        .OrderBy(r => r.IdIngrediente)
                        .Select(r => new IngredienteCarta
                        {
                            IdIngrediente = r.IdIngrediente,
                            Nombre = r.Ingrediente.Nombre,
                            Quitable = r.Quitable
                        })
                        .ToList()
                }
            })
            .ToListAsync();

        // las empanadas no traen receta ni precio: en la carta van solo con el
        // nombre, y lo que se cobra es el pack
        var gustos = await _contexto.Productos
            .Where(x => x.Familia == Familia.Empanada)
            .OrderBy(x => x.IdProducto)
            .Select(x => new Gusto { IdProducto = x.IdProducto, Nombre = x.Nombre, Activo = x.Activo })
            .ToListAsync();

        var packs = await _contexto.Packs
            .Where(x => x.Activo)
            .OrderBy(x => x.Unidades)
            .Select(x => new TamanoPack { Unidades = x.Unidades, Precio = x.Precio })
            .ToListAsync();

        return View(new CartaVm
        {
            Pizzas = [.. renglones.Where(x => x.Familia == Familia.Pizza).Select(x => x.Renglon)],
            Focaccias = [.. renglones.Where(x => x.Familia == Familia.Focaccia).Select(x => x.Renglon)],
            Gustos = gustos,
            Packs = packs
        });
    }
}
