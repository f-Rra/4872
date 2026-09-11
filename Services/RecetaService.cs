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
                // en cuantas piezas se saco cada ingrediente, no en cuantos
                // renglones: dos margaritas sin albahaca son dos, no una
                Sin = g.SelectMany(x => x.Sacados.Select(i => new { Ingrediente = i, x.Cantidad }))
                    .GroupBy(x => x.Ingrediente)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.Cantidad)),
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

    public async Task<ListaDeCompras> FaltaComprar()
    {
        // cuántas piezas de cada producto hay que hacer. Un pack de doce son
        // doce empanadas: lo que se arma, no lo que se cobra
        var piezas = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Nuevo || x.Pedido.Estado == EstadoPedido.Preparando)
            .GroupBy(x => x.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cuantas = g.Sum(x => x.Cantidad * (x.UnidadesPorPack ?? 1)) })
            .ToDictionaryAsync(x => x.IdProducto, x => x.Cuantas);

        if (piezas.Count == 0)
        {
            return new ListaDeCompras();
        }

        var ids = piezas.Keys.ToList();

        // Lo que lleva cada producto por unidad, y lo que lleva su base por
        // tanda. Van por separado porque se cuentan distinto: la receta del
        // producto es de una pizza y la de la base es de la amasada entera.
        var deProducto = await _contexto.ProductoIngredientes
            .Where(x => ids.Contains(x.IdProducto))
            .Select(x => new { x.IdProducto, x.IdIngrediente, x.Cantidad })
            .ToListAsync();

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

        // cuánto se necesita de cada ingrediente, y si esa cuenta está completa
        var necesita = new Dictionary<int, decimal>();
        var conMedida = new HashSet<int>();
        var sinMedida = new HashSet<int>();

        void Sumar(int idIngrediente, decimal porPieza, int cuantasPiezas)
        {
            if (porPieza > 0)
            {
                conMedida.Add(idIngrediente);
                necesita[idIngrediente] = necesita.GetValueOrDefault(idIngrediente) + porPieza * cuantasPiezas;
            }
            else
            {
                // está en la receta pero nadie cargó cuánto: el total que
                // salga va a ser menor que el de verdad
                sinMedida.Add(idIngrediente);
            }
        }

        foreach (var r in deProducto)
        {
            Sumar(r.IdIngrediente, r.Cantidad, piezas[r.IdProducto]);
        }

        foreach (var r in deBase)
        {
            // el rinde no puede ser cero, pero si alguien lo carga en cero la
            // division rompe la pantalla entera por un dato mal puesto
            var porUnidad = r.Rinde > 0 ? r.Cantidad / r.Rinde : 0m;
            Sumar(r.IdIngrediente, porUnidad, piezas[r.IdProducto]);
        }

        var ingredientes = await _contexto.Ingredientes
            .Where(x => necesita.Keys.Contains(x.IdIngrediente) || sinMedida.Contains(x.IdIngrediente))
            .Select(x => new { x.IdIngrediente, x.Nombre, x.Stock, x.Libre, x.Unidad })
            .ToListAsync();

        var faltan = ingredientes
            // el agua y la masa madre no se compran: contarlas seria mandarlo a
            // comprar algo que no se compra
            .Where(x => !x.Libre && conMedida.Contains(x.IdIngrediente))
            .Select(x => new
            {
                x.Nombre,
                x.Unidad,
                Falta = necesita[x.IdIngrediente] - x.Stock,
                Flojo = sinMedida.Contains(x.IdIngrediente)
            })
            .Where(x => x.Falta > 0)
            // primero lo que mas falta: es el orden en que se hace una compra
            .OrderByDescending(x => x.Falta)
            .ThenBy(x => x.Nombre)
            .Select(x => new RenglonComprar
            {
                Nombre = x.Nombre,
                Cuanto = Cantidades.Bonito(x.Falta, x.Unidad),
                Flojo = x.Flojo
            })
            .ToList();

        return new ListaDeCompras
        {
            Renglones = faltan,
            // los que entran en algun pedido y no tienen ninguna medida cargada:
            // de esos no se puede decir nada, ni que alcanza ni que falta
            SinMedida = ingredientes.Count(x =>
                !x.Libre && sinMedida.Contains(x.IdIngrediente) && !conMedida.Contains(x.IdIngrediente))
        };
    }
}
