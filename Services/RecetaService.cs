using f4872.Data;
using f4872.Helpers;
using f4872.Models;
using f4872.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace f4872.Services;

// Cuánto de cada ingrediente se come lo que hay pedido, y qué falta comprar.
//
// Vive acá y no en el controlador porque la misma cuenta la van a necesitar
// Producción, Ingredientes y Costos: las cuatro pantallas preguntan lo mismo
// —cuánto hace falta de cada cosa— y solo cambia qué hacen con la respuesta.
public class RecetaService
{
    private readonly Contexto _contexto;

    public RecetaService(Contexto contexto)
    {
        _contexto = contexto;
    }

    // Lo que hay que hacer, juntando todos los pedidos sin entregar.
    //
    // Devuelve el desglose entero -que se le saco a cada producto y de que packs
    // salieron las empanadas- y cada pantalla usa lo que le sirve: el Inicio
    // muestra los totales, Produccion abre el detalle. Una sola consulta y una
    // sola manera de contar, para que las dos no puedan decir cosas distintas.
    public async Task<Consolidado> Consolidar()
    {
        var items = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Nuevo || x.Pedido.Estado == EstadoPedido.Preparando)
            .Select(x => new
            {
                x.IdProducto,
                x.Producto.Nombre,
                x.Producto.Familia,
                x.Cantidad,
                x.UnidadesPorPack,
                Sacados = x.Quitados.Select(q => q.Ingrediente).ToList()
            })
            .ToListAsync();

        var productos = items
            .GroupBy(x => new { x.IdProducto, x.Nombre, x.Familia })
            .Select(g => new ProductoPedido
            {
                IdProducto = g.Key.IdProducto,
                Nombre = g.Key.Nombre,
                Familia = g.Key.Familia,
                // un pack de doce son doce empanadas: se cocinan unidades
                Piezas = g.Sum(x => x.Cantidad * (x.UnidadesPorPack ?? 1)),
                // Agrupado por combinacion y no por ingrediente. Una pizza sin
                // albahaca y oliva es UNA manera de armarla, no dos: contarla en
                // los dos ingredientes la cuenta dos veces y esconde las que van
                // enteras. Asi los renglones suman exactamente las piezas.
                Combinaciones = Variantes(g.Select(x => (x.Cantidad, x.Sacados))),
                Packs = g.Where(x => x.UnidadesPorPack is not null)
                    .GroupBy(x => x.UnidadesPorPack!.Value)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.Cantidad))
            })
            // el orden de la carta. Familia se guarda como texto, asi que este
            // OrderBy tiene que ser en memoria: en SQL saldria alfabetico
            .OrderBy(x => x.Familia)
            .ThenBy(x => x.IdProducto)
            .ToList();

        return new Consolidado { Productos = productos };
    }

    // Las variantes de un producto: solo las que llevan algo sacado, con cuantas
    // piezas van de cada una.
    //
    // Las que van enteras no se nombran. Si el renglon dice 5 y abajo hay «2 sin
    // albahaca, oliva» y «1 sin albahaca», las 2 que faltan salen por resta, y
    // un «2 con todo» seria un renglon mas para decir lo mismo.
    private static IReadOnlyList<Combinacion> Variantes(IEnumerable<(int Cantidad, List<string> Sacados)> items)
    {
        var porComo = items
            // los sacados van ordenados antes de juntarlos: «albahaca, oliva» y
            // «oliva, albahaca» son la misma pizza y tienen que caer en el mismo
            // grupo. El pedido ya los guarda ordenados, pero no depende de eso
            .GroupBy(x => string.Join(", ", x.Sacados.OrderBy(i => i)))
            // las que van enteras quedan afuera: son el resto
            .Where(g => g.Key.Length > 0)
            .Select(g => new Combinacion
            {
                Como = "sin " + g.Key.ToLowerInvariant(),
                Piezas = g.Sum(x => x.Cantidad)
            })
            // de mayor a menor: es el orden en que se arma la tanda
            .OrderByDescending(x => x.Piezas)
            .ThenBy(x => x.Como)
            .ToList();

        return porComo;
    }

    // Cuantas tandas de masa hay que amasar. La receta de la base se carga por
    // tanda entera con su rinde -1 kg de harina da 6 bollos- asi que el numero
    // que sirve en la mesada es cuantas tandas, no cuantos gramos de harina.
    //
    // Se redondea para arriba: media tanda no se amasa.
    public async Task<string> Amasado(IReadOnlyList<ProductoPedido> productos)
    {
        var piezas = productos
            .Where(x => x.Familia != Familia.Empanada)
            .ToDictionary(x => x.IdProducto, x => x.Piezas);

        if (piezas.Count == 0)
        {
            return "nada que amasar";
        }

        var ids = piezas.Keys.ToList();
        var bases = await _contexto.Productos
            .Where(x => ids.Contains(x.IdProducto) && x.IdBase != null && x.Base!.Rinde > 0)
            .Select(x => new { x.IdProducto, x.Base!.Rinde })
            .ToListAsync();

        if (bases.Count == 0)
        {
            return "sin base cargada";
        }

        // agrupado por rinde y no por base: dos bases que rinden seis se amasan
        // igual, y lo que se lee en la mesada es «cuatro tandas de seis»
        var porRinde = bases
            .GroupBy(x => x.Rinde)
            .Select(g => new
            {
                Rinde = g.Key,
                Tandas = (int)Math.Ceiling(g.Sum(x => piezas[x.IdProducto]) / (double)g.Key)
            })
            .OrderByDescending(x => x.Tandas);

        return string.Join(" · ", porRinde.Select(x =>
            $"{x.Tandas} {(x.Tandas == 1 ? "tanda" : "tandas")} de {x.Rinde}"));
    }

    // Cuanto se come de cada ingrediente lo que hay pedido.
    //
    // Es la cuenta que ya hacia FaltaComprar, pero devuelta cruda -por id, sin
    // formatear y sin filtrar- porque la pantalla de Ingredientes necesita
    // mostrar tambien los que alcanzan y los que no se compran.
    public async Task<IReadOnlyDictionary<int, decimal>> Necesita()
    {
        // renglon por renglon y no agrupado por producto: lo que cada uno lleva
        // sacado es de ese renglon, y agrupando antes se pierde
        var items = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Nuevo || x.Pedido.Estado == EstadoPedido.Preparando)
            .Select(x => new
            {
                x.IdProducto,
                Piezas = x.Cantidad * (x.UnidadesPorPack ?? 1),
                Sacados = x.Quitados.Select(q => q.Ingrediente).ToList()
            })
            .ToListAsync();

        if (items.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var piezas = items
            .GroupBy(x => x.IdProducto)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Piezas));

        var ids = piezas.Keys.ToList();

        // el nombre viene con la receta porque el quitado guarda el nombre y no
        // una clave -asi el pedido se lee aunque el ingrediente ya no exista- y
        // es por ahi por donde hay que cruzarlos
        var deProducto = await _contexto.ProductoIngredientes
            .Where(x => ids.Contains(x.IdProducto))
            .Select(x => new { x.IdProducto, x.IdIngrediente, x.Ingrediente.Nombre, x.Cantidad })
            .ToListAsync();

        var recetas = deProducto
            .GroupBy(x => x.IdProducto)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deBase = await _contexto.Productos
            .Where(x => ids.Contains(x.IdProducto) && x.IdBase != null)
            .SelectMany(p => p.Base!.Receta.Select(r => new
            {
                p.IdProducto,
                r.IdIngrediente,
                r.Cantidad,
                p.Base.Rinde
            }))
            .ToListAsync();

        var necesita = new Dictionary<int, decimal>();

        foreach (var item in items)
        {
            if (!recetas.TryGetValue(item.IdProducto, out var receta))
            {
                continue;
            }

            foreach (var r in receta)
            {
                // el que la pidio sin albahaca no se la come: contarsela igual
                // es mandarlo a comprar para cinco pizzas cuando la llevan dos
                if (item.Sacados.Contains(r.Nombre))
                {
                    continue;
                }

                necesita[r.IdIngrediente] = necesita.GetValueOrDefault(r.IdIngrediente) + r.Cantidad * item.Piezas;
            }
        }

        // la base no se descuenta: una pizza sin albahaca se hace con el bollo
        // entero igual, y de la masa no se saca nada
        foreach (var r in deBase)
        {
            // el rinde no puede ser cero, pero si alguien lo carga en cero la
            // division rompe la pantalla entera por un dato mal puesto
            var porUnidad = r.Rinde > 0 ? r.Cantidad / r.Rinde : 0m;
            necesita[r.IdIngrediente] = necesita.GetValueOrDefault(r.IdIngrediente) + porUnidad * piezas[r.IdProducto];
        }

        return necesita;
    }

    // Que hay que ir a comprar: lo que se come menos lo que hay en stock.
    //
    // Se apoya en Necesita para que la cuenta viva en un solo lado: la pantalla
    // de Ingredientes muestra la misma resta abierta en columnas.
    public async Task<ListaDeCompras> FaltaComprar()
    {
        var necesita = await Necesita();

        if (necesita.Count == 0)
        {
            return new ListaDeCompras();
        }

        var usados = necesita.Keys.ToList();
        var ingredientes = await _contexto.Ingredientes
            .Where(x => usados.Contains(x.IdIngrediente))
            .Select(x => new { x.IdIngrediente, x.Nombre, x.Stock, x.Libre, x.Unidad })
            .ToListAsync();

        return new ListaDeCompras
        {
            Renglones =
            [
                .. ingredientes
                    // el agua y la masa madre no se compran: contarlas seria
                    // mandarlo a comprar algo que no se compra
                    .Where(x => !x.Libre)
                    .Select(x => new { x.Nombre, x.Unidad, Falta = necesita[x.IdIngrediente] - x.Stock })
                    .Where(x => x.Falta > 0)
                    // primero lo que mas falta: es el orden en que se hace una compra
                    .OrderByDescending(x => x.Falta)
                    .ThenBy(x => x.Nombre)
                    .Select(x => new RenglonComprar
                    {
                        Nombre = x.Nombre,
                        Cuanto = Cantidades.Bonito(x.Falta, x.Unidad)
                    })
            ]
        };
    }

}
