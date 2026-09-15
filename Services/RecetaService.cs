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

    // «Bollo de pizza» se lee «pizza» cuando ya se dijo la palabra tanda: «2
    // tandas de bollo de pizza» dice bollo dos veces. Si el nombre no empieza
    // asi se deja entero, que es lo unico honesto con un nombre que no conozco.
    private static string Corto(string nombre) =>
        nombre.StartsWith("Bollo de ", StringComparison.OrdinalIgnoreCase)
            ? nombre["Bollo de ".Length..]
            : nombre.ToLowerInvariant();

    // Cuantas tandas hay que amasar para esas piezas. Se redondea para arriba
    // porque media tanda no se amasa, y es la misma cuenta que usa la lista de
    // compras: si se amasan tres tandas, se gasta harina para tres.
    private static int Tandas(int piezas, int rinde) =>
        rinde > 0 ? (int)Math.Ceiling(piezas / (double)rinde) : 0;

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
            .Select(x => new { x.IdProducto, x.Base!.IdBase, x.Base.Nombre, x.Base.Rinde })
            .ToListAsync();

        if (bases.Count == 0)
        {
            return "sin base cargada";
        }

        // Agrupado por base y no por rinde: la masa de pizza y la de focaccia
        // son masas distintas, y juntarlas porque las dos rindieran seis seria
        // mandarlo a amasar una sola tanda de dos cosas que no se mezclan.
        var porBase = bases
            .GroupBy(x => new { x.IdBase, x.Nombre, x.Rinde })
            .Select(g => new
            {
                g.Key.Nombre,
                Tandas = Tandas(g.Sum(x => piezas[x.IdProducto]), g.Key.Rinde)
            })
            .OrderByDescending(x => x.Tandas)
            .ThenBy(x => x.Nombre);

        return string.Join(" · ", porBase.Select(x =>
            $"{x.Tandas} {(x.Tandas == 1 ? "tanda" : "tandas")} de {Corto(x.Nombre)}"));
    }

    // De donde sale cada parte de lo que hace falta: un renglon por producto que
    // lleva el ingrediente, y uno por base.
    //
    // Es la unica cuenta. Necesita() no es mas que esto sumado por ingrediente y
    // la ficha lo muestra abierto, asi que el total de la ficha y el numero de
    // la grilla no pueden discrepar: salen del mismo lugar.
    private async Task<IReadOnlyList<ParteDeReceta>> Reparto()
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
            return [];
        }

        var piezas = items
            .GroupBy(x => x.IdProducto)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Piezas));

        var ids = piezas.Keys.ToList();

        // el nombre del ingrediente viene con la receta porque el quitado guarda
        // el nombre y no una clave -asi el pedido se lee aunque el ingrediente ya
        // no exista- y es por ahi por donde hay que cruzarlos
        var deProducto = await _contexto.ProductoIngredientes
            .Where(x => ids.Contains(x.IdProducto))
            .Select(x => new
            {
                x.IdProducto,
                Producto = x.Producto.Nombre,
                x.IdIngrediente,
                Ingrediente = x.Ingrediente.Nombre,
                x.Cantidad
            })
            .ToListAsync();

        var recetas = deProducto
            .GroupBy(x => x.IdProducto)
            .ToDictionary(g => g.Key, g => g.ToList());

        // cuantas piezas de cada producto lo llevan de verdad: el que la pidio
        // sin albahaca no se la come, y contarsela igual es mandarlo a comprar
        // para cinco pizzas cuando la llevan dos
        var llevan = new Dictionary<(int Producto, int Ingrediente), int>();

        foreach (var item in items)
        {
            if (!recetas.TryGetValue(item.IdProducto, out var receta))
            {
                continue;
            }

            foreach (var r in receta)
            {
                if (item.Sacados.Contains(r.Ingrediente))
                {
                    continue;
                }

                var clave = (r.IdProducto, r.IdIngrediente);
                llevan[clave] = llevan.GetValueOrDefault(clave) + item.Piezas;
            }
        }

        var partes = deProducto
            .Where(x => llevan.ContainsKey((x.IdProducto, x.IdIngrediente)))
            .Select(x => new ParteDeReceta
            {
                IdIngrediente = x.IdIngrediente,
                Donde = x.Producto,
                Cantidad = x.Cantidad,
                Piezas = llevan[(x.IdProducto, x.IdIngrediente)],
                Total = x.Cantidad * llevan[(x.IdProducto, x.IdIngrediente)]
            })
            .ToList();

        var deBase = await _contexto.Productos
            .Where(x => ids.Contains(x.IdProducto) && x.IdBase != null)
            .SelectMany(p => p.Base!.Receta.Select(r => new
            {
                p.IdProducto,
                Base = p.Base.Nombre,
                p.Base.Rinde,
                r.IdIngrediente,
                r.Cantidad
            }))
            .ToListAsync();

        // Agrupado por base y no por producto: el bollo es uno solo y se amasa
        // para todas las piezas que lo llevan juntas. La base tampoco se
        // descuenta: una pizza sin albahaca se hace con el bollo entero igual.
        partes.AddRange(deBase
            .GroupBy(x => new { x.Base, x.Rinde, x.IdIngrediente, x.Cantidad })
            .Select(g =>
            {
                var cuantas = g.Sum(x => piezas[x.IdProducto]);
                var tandas = Tandas(cuantas, g.Key.Rinde);

                return new ParteDeReceta
                {
                    IdIngrediente = g.Key.IdIngrediente,
                    Donde = g.Key.Base,
                    Cantidad = g.Key.Cantidad,
                    Rinde = g.Key.Rinde,
                    Piezas = cuantas,
                    Tandas = tandas,
                    // Por tandas enteras y no proporcional: media tanda no se
                    // amasa. Para trece bollos de a seis hay que hacer tres
                    // tandas, asi que se compra harina para tres.
                    Total = g.Key.Cantidad * tandas
                };
            }));

        return partes;
    }

    // Cuanto se come de cada ingrediente lo que hay pedido.
    //
    // Devuelto crudo -por id, sin formatear y sin filtrar- porque la pantalla de
    // Ingredientes necesita mostrar tambien los que alcanzan y los que no se
    // compran.
    public async Task<IReadOnlyDictionary<int, decimal>> Necesita() =>
        (await Reparto())
            .GroupBy(x => x.IdIngrediente)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

    // Lo mismo, abierto, para un solo ingrediente: de que producto sale cada
    // parte del numero que muestra la grilla.
    public async Task<IReadOnlyList<ParteDeReceta>> Desglose(int idIngrediente) =>
        (await Reparto())
            .Where(x => x.IdIngrediente == idIngrediente)
            // lo que mas pesa primero: es el renglon que explica el total
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Donde)
            .ToList();

    // Lo que cuesta hacer una unidad de cada producto y lo que deja.
    //
    // No mira los pedidos: es la receta contra el precio al que se compra cada
    // ingrediente. Vive aca porque recorre las mismas dos recetas que el resto
    // -la del producto y la de su base- y son las mismas reglas: la base se
    // carga por tanda entera y hay que dividirla por el rinde.
    public async Task<IReadOnlyList<CostoDeProducto>> Costos()
    {
        var productos = await _contexto.Productos
            .Select(p => new
            {
                p.IdProducto,
                p.Nombre,
                p.Familia,
                p.Precio,
                Base = p.Base == null ? null : new
                {
                    p.Base.Nombre,
                    p.Base.Rinde,
                    Receta = p.Base.Receta.Select(r => new
                    {
                        r.Ingrediente.Nombre,
                        r.Ingrediente.Unidad,
                        r.Ingrediente.Libre,
                        r.Ingrediente.CantidadDeCompra,
                        r.Ingrediente.PrecioDeCompra,
                        r.Cantidad
                    }).ToList()
                },
                Receta = p.Receta.Select(r => new
                {
                    r.Ingrediente.Nombre,
                    r.Ingrediente.Unidad,
                    r.Ingrediente.Libre,
                    r.Ingrediente.CantidadDeCompra,
                    r.Ingrediente.PrecioDeCompra,
                    r.Cantidad
                }).ToList()
            })
            .ToListAsync();

        // El precio por unidad de una empanada sale del pack, y de los packs se
        // toma el que sale mas barato por unidad: es el peor caso para el margen
        // y el que conviene mirar.
        var porPack = await _contexto.Packs
            .Where(x => x.Activo && x.Unidades > 0)
            .Select(x => x.Precio / x.Unidades)
            .ToListAsync();

        var deEmpanada = porPack.Count > 0 ? porPack.Min() : 0m;

        var costos = new List<CostoDeProducto>();

        foreach (var p in productos)
        {
            var renglones = new List<RenglonDeCosto>();
            var sinPrecio = 0;
            var costo = 0m;

            if (p.Base is not null)
            {
                var deLaBase = new List<RenglonDeCosto>();
                var costoBase = 0m;

                foreach (var r in p.Base.Receta)
                {
                    // la receta de la base es de la tanda entera: lo que entra en
                    // una pizza es esa cantidad dividida por el rinde
                    var cuanto = p.Base.Rinde > 0 ? r.Cantidad / p.Base.Rinde : 0m;
                    var sale = Cuanto(cuanto, r.Libre, r.CantidadDeCompra, r.PrecioDeCompra);

                    if (sale is null)
                    {
                        sinPrecio++;
                    }

                    costoBase += sale ?? 0m;

                    deLaBase.Add(new RenglonDeCosto
                    {
                        Nombre = r.Nombre,
                        Cuanto = Cantidades.Bonito(cuanto, r.Unidad),
                        Bulto = Bulto(r.CantidadDeCompra, r.PrecioDeCompra, r.Unidad),
                        Sale = sale,
                        DeLaBase = true
                    });
                }

                // el renglon de la base va primero y lleva el total de una
                // unidad; los de abajo cuelgan de el y no se vuelven a sumar
                renglones.Add(new RenglonDeCosto { Nombre = p.Base.Nombre, Sale = costoBase, EsBase = true });
                renglones.AddRange(deLaBase);
                costo += costoBase;
            }

            foreach (var r in p.Receta)
            {
                var sale = Cuanto(r.Cantidad, r.Libre, r.CantidadDeCompra, r.PrecioDeCompra);

                if (sale is null)
                {
                    sinPrecio++;
                }

                costo += sale ?? 0m;

                renglones.Add(new RenglonDeCosto
                {
                    Nombre = r.Nombre,
                    Cuanto = Cantidades.Bonito(r.Cantidad, r.Unidad),
                    Bulto = Bulto(r.CantidadDeCompra, r.PrecioDeCompra, r.Unidad),
                    Sale = sale
                });
            }

            costos.Add(new CostoDeProducto
            {
                IdProducto = p.IdProducto,
                Nombre = p.Nombre,
                Familia = p.Familia,
                Costo = costo,
                Venta = p.Familia == Familia.Empanada ? deEmpanada : p.Precio ?? 0m,
                SinPrecio = sinPrecio,
                Desglose = renglones
            });
        }

        // el peor margen arriba: es la pantalla de que revisar, no un listado
        return [.. costos.OrderBy(x => x.Porcentaje ?? int.MinValue).ThenBy(x => x.Nombre)];
    }

    // Lo cobrado contra lo que costo, mes por mes, de los pedidos entregados.
    //
    // Lo cobrado es historico de verdad: el precio se copia al item cuando se
    // confirma el pedido. El costo NO lo es -sale de los precios de compra de
    // hoy- porque el ingrediente guarda un solo precio y no una historia. Un
    // mes viejo queda costeado a precios de hoy, y eso hay que decirlo.
    public async Task<IReadOnlyList<MesDeCostos>> PorMes()
    {
        var pedidos = await _contexto.Pedidos
            .Where(x => x.Estado == EstadoPedido.Entregado)
            .Select(x => new { x.IdPedido, x.FechaPedido })
            .ToListAsync();

        if (pedidos.Count == 0)
        {
            return [];
        }

        var items = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Entregado)
            .Select(x => new
            {
                x.IdPedido,
                x.IdProducto,
                Piezas = x.Cantidad * (x.UnidadesPorPack ?? 1),
                Cobrado = x.Cantidad * x.PrecioUnitario
            })
            .ToListAsync();

        var cuestaCada = (await Costos()).ToDictionary(x => x.IdProducto, x => x.Costo);

        // el mes es el de Buenos Aires y no el de UTC: un pedido de las nueve de
        // la noche del treinta y uno cae en el mes que viene si se lo mira en UTC
        var mesDe = pedidos.ToDictionary(
            x => x.IdPedido,
            x => new DateOnly(Reloj.EnBuenosAires(x.FechaPedido).Year, Reloj.EnBuenosAires(x.FechaPedido).Month, 1));

        var ahora = Reloj.EnBuenosAires(DateTime.UtcNow);
        var esteMes = new DateOnly(ahora.Year, ahora.Month, 1);

        var porMes = items
            .GroupBy(x => mesDe[x.IdPedido])
            .ToDictionary(
                g => g.Key,
                g => (Cobro: g.Sum(x => x.Cobrado),
                      Costo: g.Sum(x => cuestaCada.GetValueOrDefault(x.IdProducto) * x.Piezas)));

        return
        [
            .. pedidos
                .GroupBy(x => mesDe[x.IdPedido])
                .Select(g => new MesDeCostos
                {
                    Mes = g.Key,
                    Pedidos = g.Count(),
                    Cobro = porMes.GetValueOrDefault(g.Key).Cobro,
                    Costo = porMes.GetValueOrDefault(g.Key).Costo,
                    // el mes en curso no esta cerrado: su margen todavia se mueve
                    Abierto = g.Key == esteMes
                })
                // del mas viejo al mas nuevo, que es como se lee una evolucion
                .OrderBy(x => x.Mes)
        ];
    }

    // Lo que sale esa cantidad de ese ingrediente. Nulo cuando no se puede
    // saber: sin el bulto o sin su precio no hay precio por gramo.
    //
    // El agua y la masa madre no son un caso sin cargar: no se compran, asi que
    // cuestan cero de verdad y no le faltan al costo.
    private static decimal? Cuanto(decimal cantidad, bool libre, decimal? bulto, decimal? precio)
    {
        if (libre)
        {
            return 0m;
        }

        return bulto > 0 && precio.HasValue ? cantidad * (precio.Value / bulto.Value) : null;
    }

    // «$32.000 cada 25 kg»: de donde sale el precio por gramo, dicho como se
    // compra en el almacen y no como se usa en la receta.
    private static string Bulto(decimal? cantidad, decimal? precio, Medida unidad) =>
        cantidad > 0 && precio.HasValue
            ? $"{precio.Value.ToString("C")} cada {Cantidades.Bonito(cantidad.Value, unidad)}"
            : "";

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
