using f4872.Data;
using f4872.Helpers;
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
    private readonly RecetaService _recetas;

    public TiendaController(Contexto contexto, PedidoService pedidos, RecetaService recetas)
    {
        _contexto = contexto;
        _pedidos = pedidos;
        _recetas = recetas;
    }

    // el inicio: existe para el que llega de cero, porque el nombre no dice que
    // esto es una pizzería ni que el pedido tarda días.
    //
    // Necesita saber si la tienda toma pedidos: cerrada, el botón invitaba a
    // armar uno que no se puede hacer y el renglón de abajo prometía una entrega
    // para el fin de semana. La carta ya lo miraba; el inicio no se había enterado.
    public async Task<IActionResult> Index()
    {
        return View(await Abierta());
    }

    // la carta. La acción se llama en español como todo el código; /shop es
    // solo la dirección que ve el cliente
    [Route("shop")]
    public async Task<IActionResult> Carta()
    {
        // Cerrada no se consulta nada: la pantalla no muestra la carta, así que
        // traerla serían cuatro consultas para tirar a la basura.
        if (!await Abierta())
        {
            return View(new CartaVm { Abierta = false });
        }

        // una sola consulta para las dos familias: son la misma forma de renglón
        // y traerlas por separado serían dos viajes a la base para nada
        // en el orden que eligió él: la posición, que se cuenta dentro de cada
        // familia, y las dos se separan abajo
        var renglones = await _contexto.Productos
            .Where(x => x.Familia == Familia.Pizza || x.Familia == Familia.Focaccia)
            .OrderBy(x => x.Posicion)
            .ThenBy(x => x.IdProducto)
            .Select(x => new
            {
                x.Familia,
                // los ocultos no se nombran: siguen en la receta por el costo y la
                // compra, pero la carta solo dice los que él dejó a la vista
                Salsas = x.Salsas.Where(s => s.Visible)
                    .Select(s => new { s.Posicion, s.IdReceta, s.Receta.Nombre, s.Quitable }).ToList(),
                Ingredientes = x.Receta.Where(r => r.Visible).Select(r => new
                {
                    r.Posicion,
                    Item = new IngredienteCarta
                    {
                        Clave = r.IdIngrediente.ToString(),
                        Nombre = r.Ingrediente.Nombre,
                        Quitable = r.Quitable
                    }
                }).ToList(),
                Renglon = new RenglonCarta
                {
                    IdProducto = x.IdProducto,
                    Nombre = x.Nombre,
                    Precio = x.Precio,
                    Activo = x.Activo
                }
            })
            .ToListAsync();

        // Las salsas se nombran junto con los ingredientes, cada una donde la
        // puso él en la ficha: es una sola lista y se ordena junta. Se arma acá
        // y no en la consulta porque no son renglones de la receta del producto.
        // Al empatar, la salsa primero.
        //
        // Se sacan igual que un ingrediente, si él la marcó modificable.
        foreach (var x in renglones)
        {
            x.Renglon.Ingredientes =
            [
                .. x.Salsas.Select(s => (s.Posicion, Salsa: 0, Id: s.IdReceta,
                        Item: new IngredienteCarta { Clave = "s" + s.IdReceta, Nombre = s.Nombre, Quitable = s.Quitable }))
                    .Concat(x.Ingredientes.Select(i => (i.Posicion, Salsa: 1, Id: int.Parse(i.Item.Clave), i.Item)))
                    .OrderBy(i => i.Posicion)
                    .ThenBy(i => i.Salsa)
                    .ThenBy(i => i.Id)
                    .Select(i => i.Item)
            ];
        }

        // las empanadas no traen receta ni precio: en la carta van solo con el
        // nombre, y lo que se cobra es el pack
        var gustos = await _contexto.Productos
            .Where(x => x.Familia == Familia.Empanada)
            .OrderBy(x => x.Posicion)
            .ThenBy(x => x.IdProducto)
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
            FichaPizzas = await Ficha(Familia.Pizza),
            FichaFocaccias = await Ficha(Familia.Focaccia),
            Gustos = gustos,
            Packs = packs
        });
    }

    // Lo que cada familia dice de sí misma arriba de su lista. El peso del bollo
    // sale de la receta de la masa y va redondeado a 50 g —288 g se dice 300—;
    // lo demás son datos del negocio que no salen de ningún lado, y por eso están
    // escritos acá. Las empanadas no llevan.
    private async Task<IReadOnlyList<DatoFicha>> Ficha(Familia familia)
    {
        var datos = new List<DatoFicha>();

        if (await _recetas.PesoDelBollo(familia) is decimal peso)
        {
            var redondo = Math.Round(peso / 50, MidpointRounding.AwayFromZero) * 50;

            if (redondo > 0)
            {
                datos.Add(new DatoFicha { Rotulo = "Bollo", Valor = Cantidades.Bonito(redondo, Medida.Gramo) });
            }
        }

        if (familia == Familia.Pizza)
        {
            datos.Add(new DatoFicha { Rotulo = "Diámetro", Valor = "27 cm" });
            datos.Add(new DatoFicha { Rotulo = "Porciones", Valor = "4" });
        }
        else if (familia == Familia.Focaccia)
        {
            datos.Add(new DatoFicha { Rotulo = "Tamaño", Valor = "20 × 30 cm" });
        }

        return datos;
    }

    // el resumen del pedido. La pantalla no recibe el pedido: lo lee del
    // navegador, que es donde vive hasta que se confirma. Lo que sí recibe es
    // la carta, para poder traducir esas claves a nombres y precios
    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout()
    {
        // se llega acá con el pedido guardado de la semana pasada y el botón de
        // atrás. La carta es la que cuenta que está cerrada, así que manda ahí
        // en vez de hacerle llenar un formulario que no va a poder mandar
        if (!await Abierta())
        {
            return RedirectToAction(nameof(Carta));
        }

        // Se ordena en memoria y no en la consulta: Familia se guarda como
        // texto, y un ORDER BY en la base ponía las focaccias antes que las
        // pizzas, al revés que la carta.
        var productos = (await _contexto.Productos
                .Where(x => x.Familia != Familia.Empanada && x.Precio != null)
                .Select(x => new { x.IdProducto, x.Nombre, Precio = x.Precio!.Value, x.Familia, x.Posicion })
                .ToListAsync())
            .OrderBy(x => x.Familia)
            .ThenBy(x => x.Posicion)
            .ThenBy(x => x.IdProducto)
            .ToList();

        var gustos = await _contexto.Productos
            .Where(x => x.Familia == Familia.Empanada)
            .OrderBy(x => x.Posicion)
            .ThenBy(x => x.IdProducto)
            .Select(x => new { x.IdProducto, x.Nombre })
            .ToListAsync();

        var packs = await _contexto.Packs
            .Where(x => x.Activo)
            .ToDictionaryAsync(x => x.Unidades, x => x.Precio);

        // el mismo ingrediente puede ser quitable en varias recetas: se piden
        // distintos para no traer el nombre repetido una vez por producto
        var ingredientes = await _contexto.ProductoIngredientes
            .Where(x => x.Quitable)
            .Select(x => new { x.IdIngrediente, x.Ingrediente.Nombre })
            .Distinct()
            .ToDictionaryAsync(x => x.IdIngrediente.ToString(), x => x.Nombre);

        // las salsas que se pueden sacar, con la marca de su clave: el mismo
        // diccionario traduce las dos cosas
        foreach (var s in await _contexto.ProductoSalsas
                     .Where(x => x.Quitable)
                     .Select(x => new { x.IdReceta, x.Receta.Nombre })
                     .Distinct()
                     .ToListAsync())
        {
            ingredientes["s" + s.IdReceta] = s.Nombre;
        }

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
            Gustos = gustos
                .Select((x, i) => new { x.IdProducto, Dato = new GustoDelPedido { Nombre = x.Nombre, Orden = i } })
                .ToDictionary(x => x.IdProducto, x => x.Dato),
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

    // La confirmacion. El numero va en la direccion a proposito: es lo unico
    // que el cliente puede necesitar repetir, y asi la pantalla se puede volver
    // a abrir.
    //
    // No devuelve nada del pedido salvo que existe. El saludo con el nombre lo
    // pone el navegador: los numeros son correlativos, y un nombre que saliera
    // de aca convertiria /gracias/1, /gracias/2 en una lista de clientes.
    [HttpGet("gracias/{id:int}")]
    public async Task<IActionResult> Gracias(int id)
    {
        if (!await _contexto.Pedidos.AnyAsync(x => x.IdPedido == id))
        {
            return NotFound();
        }

        return View(id);
    }

    // el estado de la tienda: una sola fila, y la unica pregunta que se le hace
    private async Task<bool> Abierta() =>
        await _contexto.Tienda.AnyAsync(x => x.Abierta);
}
