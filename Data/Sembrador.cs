using f4872.Models;
using Microsoft.EntityFrameworkCore;

namespace f4872.Data;

/// <summary>
/// CARTA INVENTADA. Ninguna de estas pizzas, focaccias, gustos, precios,
/// cantidades ni compras es real: salen de las maquetas, donde se inventaron
/// para poder diseñar. Lo único real acá es la receta del bollo.
/// Corre solo en Development y solo si la base está vacía.
/// </summary>
public static class Sembrador
{
    // los que hacen al producto: sin ellos deja de ser eso. El resto se puede sacar
    private static readonly HashSet<string> Fijos =
    [
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

    // la receta del bollo es la única cosa real de todo este archivo
    private static readonly (string N, int Rinde, (string Ing, decimal Cant)[] Receta)[] LasBases =
    [
        ("Bollo de masa",    6, [("Harina 000", 1000), ("Agua", 700), ("Masa madre", 100), ("Sal fina", 30)]),
        ("Tapa de empanada", 1, [("Tapas de empanada", 1)])
    ];

    // familia, nombre, precio (nulo en empanadas: van por pack), si está en la
    // carta, la base que consume, y cuánto lleva UNA pieza de cada ingrediente
    private static readonly (Familia Fam, string N, decimal? Precio, bool Activo, string Base, (string Ing, decimal Cant)[] Receta)[] LosProductos =
    [
        (Familia.Pizza, "Margarita",      9800,  true,  "Bollo de masa",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Albahaca", 4), ("Oliva", 8)]),
        (Familia.Pizza, "Marinara",       8900,  true,  "Bollo de masa",
            [("Salsa de tomate", 90), ("Ajo", 6), ("Orégano", 1), ("Oliva", 10)]),
        (Familia.Pizza, "Fugazzeta",      11200, true,  "Bollo de masa",
            [("Muzzarella", 140), ("Cebolla", 200), ("Orégano", 1), ("Oliva", 8)]),
        (Familia.Pizza, "Napolitana",     10800, true,  "Bollo de masa",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Tomate", 90), ("Ajo", 5)]),
        (Familia.Pizza, "Calabresa",      12400, false, "Bollo de masa",
            [("Salsa de tomate", 80), ("Muzzarella", 120), ("Longaniza", 60), ("Morrón", 40)]),
        (Familia.Pizza, "Cuatro quesos",  12900, true,  "Bollo de masa",
            [("Muzzarella", 90), ("Provolone", 40), ("Roquefort", 35), ("Parmesano", 25)]),
        (Familia.Pizza, "Rúcula y crudo", 13500, true,  "Bollo de masa",
            [("Muzzarella", 110), ("Rúcula", 20), ("Jamón crudo", 40), ("Parmesano", 15)]),

        (Familia.Focaccia, "Romero y sal", 6800, true,  "Bollo de masa",
            [("Romero", 2), ("Sal gruesa", 3), ("Oliva", 12), ("Semolín", 5)]),
        (Familia.Focaccia, "Cherry",       7900, true,  "Bollo de masa",
            [("Tomate cherry", 60), ("Albahaca", 3), ("Oliva", 10), ("Sal", 1)]),
        (Familia.Focaccia, "Cebolla",      7400, true,  "Bollo de masa",
            [("Cebolla", 150), ("Tomillo", 2), ("Oliva", 10), ("Sal", 1)]),
        (Familia.Focaccia, "Aceitunas",    8200, false, "Bollo de masa",
            [("Aceitunas", 50), ("Orégano", 1), ("Oliva", 10), ("Sal", 1)]),

        (Familia.Empanada, "Carne suave",     null, true,  "Tapa de empanada",
            [("Carne", 35), ("Cebolla", 15), ("Huevo", 6), ("Comino", 1)]),
        (Familia.Empanada, "Carne picante",   null, true,  "Tapa de empanada",
            [("Carne", 35), ("Cebolla", 15), ("Pimentón", 1), ("Ají molido", 1)]),
        (Familia.Empanada, "Jamón y queso",   null, true,  "Tapa de empanada",
            [("Jamón", 20), ("Muzzarella", 25), ("Orégano", 1)]),
        (Familia.Empanada, "Humita",          null, true,  "Tapa de empanada",
            [("Choclo", 30), ("Salsa blanca", 20), ("Cebolla de verdeo", 0.2m), ("Nuez moscada", 0.2m)]),
        (Familia.Empanada, "Verdura",         null, true,  "Tapa de empanada",
            [("Acelga", 0.3m), ("Cebolla", 10), ("Salsa blanca", 15)]),
        (Familia.Empanada, "Pollo",           null, false, "Tapa de empanada",
            [("Pollo", 30), ("Cebolla", 12), ("Morrón", 8), ("Perejil", 0.2m)]),
        (Familia.Empanada, "Caprese",         null, true,  "Tapa de empanada",
            [("Muzzarella", 25), ("Tomate", 20), ("Albahaca", 2), ("Oliva", 3)]),
        (Familia.Empanada, "Cebolla y queso", null, true,  "Tapa de empanada",
            [("Cebolla", 30), ("Muzzarella", 25), ("Orégano", 1)])
    ];

    public static async Task SembrarSiEstaVacia(Contexto contexto, ILogger logger)
    {
        // si ya hay algo cargado no se toca nada: el dia que entre la carta de
        // verdad, esto no puede volver a meterle las pizzas inventadas
        if (await contexto.Productos.AnyAsync() || await contexto.Ingredientes.AnyAsync())
        {
            return;
        }

        var ingredientes = LosIngredientes.ToDictionary(
            x => x.N,
            x => new Ingrediente
            {
                Nombre = x.N,
                Unidad = x.U,
                Stock = x.Stock,
                CantidadDeCompra = x.Trae,
                PrecioDeCompra = x.Sale,
                Libre = x.Libre
            });
        contexto.Ingredientes.AddRange(ingredientes.Values);

        var bases = LasBases.ToDictionary(
            x => x.N,
            x => new Base
            {
                Nombre = x.N,
                Rinde = x.Rinde,
                Receta = [.. x.Receta.Select(r => new BaseIngrediente
                {
                    Ingrediente = ingredientes[r.Ing],
                    Cantidad = r.Cant
                })]
            });
        contexto.Bases.AddRange(bases.Values);

        contexto.Productos.AddRange(LosProductos.Select(p => new Producto
        {
            Familia = p.Fam,
            Nombre = p.N,
            Precio = p.Precio,
            Activo = p.Activo,
            Base = bases[p.Base],
            Receta = [.. p.Receta.Select(r => new ProductoIngrediente
            {
                Ingrediente = ingredientes[r.Ing],
                Cantidad = r.Cant,
                Quitable = !Fijos.Contains(r.Ing)
            })]
        }));

        await contexto.SaveChangesAsync();

        logger.LogWarning(
            "Sembrada la carta INVENTADA de la maqueta: {Productos} productos, {Ingredientes} " +
            "ingredientes y {Bases} bases. Ninguno de esos nombres ni precios es real, salvo la " +
            "receta del bollo. Para borrarla: TRUNCATE \"Productos\", \"Ingredientes\", \"Bases\" CASCADE",
            LosProductos.Length, LosIngredientes.Length, LasBases.Length);
    }
}
