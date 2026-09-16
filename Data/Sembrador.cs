using f4872.Models;
using Microsoft.EntityFrameworkCore;

namespace f4872.Data;

/// <summary>
/// CARTA INVENTADA. Ninguna de estas pizzas, focaccias, gustos, precios,
/// cantidades ni compras es real: salen de las maquetas, donde se inventaron
/// para poder diseñar. Lo único real acá son las dos recetas de bollo.
///
/// Por eso se siembra en dos partes. <see cref="SembrarLasMasas"/> corre
/// siempre, también en producción, porque las masas son del negocio.
/// <see cref="SembrarLaCartaDePrueba"/> corre solo en Development.
/// </summary>
public static class Sembrador
{
    // los que hacen al producto: sin ellos deja de ser eso. El resto se puede sacar
    private static readonly HashSet<string> Fijos =
    [
        "Tapas de empanada",
        "Salsa de tomate", "Muzzarella", "Longaniza", "Jamón crudo",
        "Carne", "Pollo", "Jamón", "Choclo", "Salsa blanca", "Acelga", "Semolín"
    ];

    // nombre, unidad, stock, cuánto trae la compra, cuánto sale, si no se compra
    private static readonly (string N, Medida U, decimal Stock, decimal? Trae, decimal? Sale, bool Libre)[] LosIngredientes =
    [
        ("Harina 000",        Medida.Gramo,     42000, 25000, 32000, false),
        ("Agua",              Medida.Mililitro,     0,  null,  null, true),
        ("Masa madre",        Medida.Gramo,       400,  null,  null, true),
        ("Sal fina",          Medida.Gramo,      2000,  1000,  1100, false),
        ("Tapas de empanada", Medida.Unidad,      120,    60,  9000, false),

        ("Salsa de tomate",   Medida.Gramo,     14000,  4000,  9600, false),
        ("Muzzarella",        Medida.Gramo,      8500,  3000, 27600, false),
        ("Albahaca",          Medida.Unidad,        0,    30,  1200, false),
        ("Oliva",             Medida.Mililitro,  3000,  5000, 57500, false),
        ("Ajo",               Medida.Gramo,       900,  1000,  3000, false),
        ("Orégano",           Medida.Gramo,       600,   500,  6000, false),
        ("Cebolla",           Medida.Gramo,     11000, 10000, 13000, false),
        ("Tomate",            Medida.Gramo,      3000,  5000,  9000, false),
        ("Longaniza",         Medida.Gramo,         0,  1000, 12000, false),
        ("Morrón",            Medida.Gramo,         0,  1000,  5200, false),
        ("Provolone",         Medida.Gramo,         0,  1000, 14000, false),
        ("Roquefort",         Medida.Gramo,       400,  1000, 16000, false),
        ("Parmesano",         Medida.Gramo,         0,  1000, 22000, false),
        ("Rúcula",            Medida.Gramo,         0,  1000,  4000, false),
        ("Jamón crudo",       Medida.Gramo,         0,  1000, 26000, false),

        ("Romero",            Medida.Gramo,       150,   200,  1800, false),
        ("Sal gruesa",        Medida.Gramo,      9000,  5000,  4000, false),
        ("Semolín",           Medida.Gramo,      1800,  5000,  7500, false),
        ("Tomate cherry",     Medida.Gramo,      2000,  2000,  7200, false),
        ("Sal",               Medida.Gramo,      4000,  1000,   900, false),
        ("Tomillo",           Medida.Gramo,       120,   200,  2100, false),
        ("Aceitunas",         Medida.Gramo,         0,  1000,  8900, false),

        ("Carne",             Medida.Gramo,      4200,  5000, 49000, false),
        ("Huevo",             Medida.Unidad,       24,    30,  7500, false),
        ("Comino",            Medida.Gramo,       200,   250,  4500, false),
        ("Pimentón",          Medida.Gramo,       180,   250,  3900, false),
        ("Ají molido",        Medida.Gramo,       150,   250,  4200, false),
        ("Jamón",             Medida.Gramo,      1200,  2000, 17000, false),
        ("Choclo",            Medida.Gramo,      6000,  3000,  6300, false),
        ("Salsa blanca",      Medida.Gramo,         0,  2000,  6400, false),
        ("Cebolla de verdeo", Medida.Unidad,        0,    12,  3600, false),
        ("Nuez moscada",      Medida.Gramo,        40,    50,  5000, false),
        ("Pollo",             Medida.Gramo,         0,  2000,  9800, false),
        ("Perejil",           Medida.Unidad,        0,    12,  2400, false),
        ("Acelga",            Medida.Unidad,        0,     6,  3000, false)
    ];

    // los dos tamaños son de verdad; los precios, inventados como todo lo demás
    private static readonly (int Unidades, decimal Precio)[] LosPacks =
    [
        (6, 7900),
        (12, 15200)
    ];

    // Las dos recetas de bollo son lo único real de todo este archivo. Van por
    // tanda entera, que es como se amasan, y el panel divide por el rinde.
    //
    // El rinde es cuantos bollos sale la tanda, y el peso del bollo sale de ahi:
    // la de pizza pesa 1730 g y se corta en 6, o sea bollos de 288; la de
    // focaccia pesa 1930 y se corta en 4, o sea de 483.
    //
    // La tapa de empanada no es una base: se compra hecha. Es un ingrediente
    // más de cada gusto, como la muzzarella.
    private static readonly (string N, Familia Fam, int Rinde, (string Ing, decimal Cant)[] Receta)[] LasBases =
    [
        ("Bollo de pizza",    Familia.Pizza,    6, [("Harina 000", 1000), ("Agua", 600), ("Masa madre", 100), ("Sal fina", 30)]),
        ("Bollo de focaccia", Familia.Focaccia, 4, [("Harina 000", 1000), ("Agua", 750), ("Masa madre", 100), ("Sal fina", 30), ("Oliva", 50)])
    ];

    // Los ingredientes que llevan las masas, sacados de LasBases para que no
    // haya dos listas que se puedan desincronizar. De la tabla de arriba se usa
    // solo el nombre, la unidad y si se compra: el stock y el precio de ahí son
    // inventados y los carga él desde Ingredientes.
    private static (string N, Medida U, bool Libre)[] LosDeLasMasas() =>
    [
        .. LasBases
            .SelectMany(b => b.Receta.Select(r => r.Ing))
            .Distinct()
            .Select(n => LosIngredientes.First(x => x.N == n))
            .Select(x => (x.N, x.U, x.Libre))
    ];

    // familia, nombre, precio (nulo en empanadas: van por pack), si está en la
    // carta, la base que consume, y cuánto lleva UNA pieza de cada ingrediente
    private static readonly (Familia Fam, string N, decimal? Precio, bool Activo, string? Base, (string Ing, decimal Cant)[] Receta)[] LosProductos =
    [
        (Familia.Pizza, "Margarita",      9800,  true,  "Bollo de pizza",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Albahaca", 4), ("Oliva", 8)]),
        (Familia.Pizza, "Marinara",       8900,  true,  "Bollo de pizza",
            [("Salsa de tomate", 90), ("Ajo", 6), ("Orégano", 1), ("Oliva", 10)]),
        (Familia.Pizza, "Fugazzeta",      11200, true,  "Bollo de pizza",
            [("Muzzarella", 140), ("Cebolla", 200), ("Orégano", 1), ("Oliva", 8)]),
        (Familia.Pizza, "Napolitana",     10800, true,  "Bollo de pizza",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Tomate", 90), ("Ajo", 5)]),
        (Familia.Pizza, "Calabresa",      12400, false, "Bollo de pizza",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Longaniza", 60), ("Morrón", 40)]),
        (Familia.Pizza, "Cuatro quesos",  12900, true,  "Bollo de pizza",
            [("Muzzarella", 90), ("Provolone", 40), ("Roquefort", 35), ("Parmesano", 25)]),
        (Familia.Pizza, "Rúcula y crudo", 13500, true,  "Bollo de pizza",
            [("Muzzarella", 110), ("Rúcula", 20), ("Jamón crudo", 40), ("Parmesano", 15)]),

        (Familia.Focaccia, "Romero y sal", 6800, true,  "Bollo de focaccia",
            [("Romero", 2), ("Sal gruesa", 3), ("Oliva", 12), ("Semolín", 5)]),
        (Familia.Focaccia, "Cherry",       7900, true,  "Bollo de focaccia",
            [("Tomate cherry", 60), ("Albahaca", 3), ("Oliva", 10), ("Sal", 1)]),
        (Familia.Focaccia, "Cebolla",      7400, true,  "Bollo de focaccia",
            [("Cebolla", 150), ("Tomillo", 2), ("Oliva", 10), ("Sal", 1)]),
        (Familia.Focaccia, "Aceitunas",    8200, false, "Bollo de focaccia",
            [("Aceitunas", 50), ("Orégano", 1), ("Oliva", 10), ("Sal", 1)]),

        (Familia.Empanada, "Carne suave",     null, true,  null,
            [("Tapas de empanada", 1), ("Carne", 35), ("Cebolla", 15), ("Huevo", 6), ("Comino", 1)]),
        (Familia.Empanada, "Carne picante",   null, true,  null,
            [("Tapas de empanada", 1), ("Carne", 35), ("Cebolla", 15), ("Pimentón", 1), ("Ají molido", 1)]),
        (Familia.Empanada, "Jamón y queso",   null, true,  null,
            [("Tapas de empanada", 1), ("Jamón", 20), ("Muzzarella", 25), ("Orégano", 1)]),
        (Familia.Empanada, "Humita",          null, true,  null,
            [("Tapas de empanada", 1), ("Choclo", 30), ("Salsa blanca", 20), ("Cebolla de verdeo", 0.2m), ("Nuez moscada", 0.2m)]),
        (Familia.Empanada, "Verdura",         null, true,  null,
            [("Tapas de empanada", 1), ("Acelga", 0.3m), ("Cebolla", 10), ("Salsa blanca", 15)]),
        (Familia.Empanada, "Pollo",           null, false, null,
            [("Tapas de empanada", 1), ("Pollo", 30), ("Cebolla", 12), ("Morrón", 8), ("Perejil", 0.2m)]),
        (Familia.Empanada, "Caprese",         null, true,  null,
            [("Tapas de empanada", 1), ("Muzzarella", 25), ("Tomate", 20), ("Albahaca", 2), ("Oliva", 3)]),
        (Familia.Empanada, "Cebolla y queso", null, true,  null,
            [("Tapas de empanada", 1), ("Cebolla", 30), ("Muzzarella", 25), ("Orégano", 1)])
    ];

    /// <summary>
    /// Las dos masas y los cinco ingredientes que llevan. Corre SIEMPRE, también
    /// en producción: no son datos de prueba, son la receta del vendedor.
    ///
    /// Sin esto, en una base nueva la primera pizza que se cargue queda sin masa
    /// —BaseDe no tiene de dónde copiarla— y a partir de ahí Producción no pide
    /// harina y el costo sale sin el renglón de la base.
    /// </summary>
    public static async Task SembrarLasMasas(Contexto contexto, ILogger logger)
    {
        if (await contexto.Bases.AnyAsync())
        {
            return;
        }

        // el ingrediente puede existir ya, cargado a mano o por la carta de
        // prueba: se reusa en vez de duplicarlo, que partiria los costos en dos
        var nombres = LosDeLasMasas().Select(x => x.N).ToList();
        var ingredientes = await contexto.Ingredientes
            .Where(x => nombres.Contains(x.Nombre))
            .ToDictionaryAsync(x => x.Nombre);

        foreach (var (n, u, libre) in LosDeLasMasas())
        {
            if (ingredientes.ContainsKey(n))
            {
                continue;
            }

            // sin stock ni precio de compra: esos son suyos y no se inventan
            var nuevo = new Ingrediente { Nombre = n, Unidad = u, Libre = libre };
            contexto.Ingredientes.Add(nuevo);
            ingredientes[n] = nuevo;
        }

        contexto.Bases.AddRange(LasBases.Select(b => new Base
        {
            Nombre = b.N,
            Familia = b.Fam,
            Rinde = b.Rinde,
            Receta = [.. b.Receta.Select(r => new BaseIngrediente
            {
                Ingrediente = ingredientes[r.Ing],
                Cantidad = r.Cant
            })]
        }));

        await contexto.SaveChangesAsync();

        logger.LogInformation(
            "Sembradas las {Cuantas} masas con sus {Ingredientes} ingredientes. Es la receta de " +
            "verdad; les falta cargar el stock y el precio de compra desde Ingredientes.",
            LasBases.Length, LosDeLasMasas().Length);
    }

    public static async Task SembrarLaCartaDePrueba(Contexto contexto, ILogger logger)
    {
        // los packs tienen guarda propia: son dos filas que la pantalla de
        // empanadas necesita para poder mostrar un precio, y se suman despues
        // de la carta. Con una sola guarda, quien ya tenia la base sembrada se
        // quedaba sin ellos
        if (!await contexto.Packs.AnyAsync())
        {
            contexto.Packs.AddRange(LosPacks.Select(p => new Pack
            {
                Unidades = p.Unidades,
                Precio = p.Precio
            }));
            await contexto.SaveChangesAsync();
            logger.LogWarning(
                "Sembrados {Cuantos} tamanos de pack con precios INVENTADOS.", LosPacks.Length);
        }

        // Si ya hay productos, o ingredientes que no sean los de las masas,
        // alguien empezo a cargar lo de verdad: no se le meten las pizzas
        // inventadas encima. La guarda no puede ser «hay ingredientes» a secas
        // porque las masas ya dejaron los cinco suyos.
        if (await contexto.Productos.AnyAsync() ||
            await contexto.Ingredientes.CountAsync() > LosDeLasMasas().Length)
        {
            return;
        }

        // las masas ya estan sembradas: corren antes y siempre
        var bases = await contexto.Bases.ToDictionaryAsync(x => x.Nombre);

        var ingredientes = await contexto.Ingredientes.ToDictionaryAsync(x => x.Nombre);
        foreach (var x in LosIngredientes)
        {
            // los de las masas ya existen, sin compra cargada. En la maquina de
            // uno conviene que tengan numeros, si no las pantallas no muestran nada
            if (ingredientes.TryGetValue(x.N, out var ya))
            {
                ya.Stock = x.Stock;
                ya.CantidadDeCompra = x.Trae;
                ya.PrecioDeCompra = x.Sale;
                continue;
            }

            var nuevo = new Ingrediente
            {
                Nombre = x.N,
                Unidad = x.U,
                Stock = x.Stock,
                CantidadDeCompra = x.Trae,
                PrecioDeCompra = x.Sale,
                Libre = x.Libre
            };
            contexto.Ingredientes.Add(nuevo);
            ingredientes[x.N] = nuevo;
        }

        contexto.Productos.AddRange(LosProductos.Select(p => new Producto
        {
            Familia = p.Fam,
            Nombre = p.N,
            Precio = p.Precio,
            Activo = p.Activo,
            // las empanadas no llevan: la tapa es un ingrediente suyo
            Base = p.Base is null ? null : bases[p.Base],
            Receta = [.. p.Receta.Select(r => new ProductoIngrediente
            {
                Ingrediente = ingredientes[r.Ing],
                Cantidad = r.Cant,
                Quitable = !Fijos.Contains(r.Ing)
            })]
        }));

        await contexto.SaveChangesAsync();

        logger.LogWarning(
            "Sembrada la carta INVENTADA de la maqueta: {Productos} productos y {Ingredientes} " +
            "ingredientes. Ninguno de esos nombres ni precios es real. Las masas no salen de " +
            "aca: esas son de verdad. Para borrar lo inventado: " +
            "TRUNCATE \"Productos\", \"Ingredientes\", \"Bases\" CASCADE",
            LosProductos.Length, LosIngredientes.Length);
    }
}
