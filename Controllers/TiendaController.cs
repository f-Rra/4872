using f4872.Data;
using f4872.Models;
using f4872.Services;
using f4872.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace f4872.Controllers;

public class TiendaController : Controller
{
    private readonly Contexto _contexto;
    private readonly PedidoService _pedidos;

    public TiendaController(Contexto contexto, PedidoService pedidos)
    {
        _contexto = contexto;
        _pedidos = pedidos;
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

    // el resumen del pedido. La pantalla no recibe el pedido: lo lee del
    // navegador, que es donde vive hasta que se confirma. Lo que sí recibe es
    // la carta, para poder traducir esas claves a nombres y precios
    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout()
    {
        var productos = await _contexto.Productos
            .Where(x => x.Familia != Familia.Empanada && x.Precio != null)
            .OrderBy(x => x.Familia)
            .ThenBy(x => x.IdProducto)
            .Select(x => new { x.IdProducto, x.Nombre, Precio = x.Precio!.Value })
            .ToListAsync();

        var gustos = await _contexto.Productos
            .Where(x => x.Familia == Familia.Empanada)
            .Select(x => new { x.IdProducto, x.Nombre })
            .ToDictionaryAsync(x => x.IdProducto, x => x.Nombre);

        var packs = await _contexto.Packs
            .Where(x => x.Activo)
            .ToDictionaryAsync(x => x.Unidades, x => x.Precio);

        // el mismo ingrediente puede ser quitable en varias recetas: se piden
        // distintos para no traer el nombre repetido una vez por producto
        var ingredientes = await _contexto.ProductoIngredientes
            .Where(x => x.Quitable)
            .Select(x => new { x.IdIngrediente, x.Ingrediente.Nombre })
            .Distinct()
            .ToDictionaryAsync(x => x.IdIngrediente, x => x.Nombre);

        return View(new CheckoutVm
        {
            Productos = productos
                .Select((x, i) => new { x.IdProducto, Dato = new ProductoDelPedido
                {
                    Nombre = x.Nombre,
                    Precio = x.Precio,
                    Orden = i
                } })
                .ToDictionary(x => x.IdProducto, x => x.Dato),
            Gustos = gustos,
            Packs = packs,
            Ingredientes = ingredientes
        });
    }

    // Confirmar el pedido. Recibe las mismas claves que guarda el navegador y
    // nada mas: el servicio les vuelve a poner precio leyendo la base.
    //
    // Sin antiforgery a proposito: el token protege de que otro sitio use la
    // sesion de alguien, y aca no hay sesion ni nada que robar. Cuando exista
    // el panel, ese si lo lleva.
    [HttpPost("checkout")]
    public async Task<IActionResult> Confirmar([FromBody] PedidoNuevo datos)
    {
        try
        {
            return Ok(new { id = await _pedidos.Confirmar(datos) });
        }
        catch (InvalidOperationException e)
        {
            // el mensaje esta escrito para que lo lea una persona, asi que sale
            // tal cual a la pantalla
            return BadRequest(new { error = e.Message });
        }
    }
}
