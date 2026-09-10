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
