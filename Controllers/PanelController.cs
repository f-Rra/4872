using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using f4872.Data;
using f4872.Helpers;
using f4872.Models;
using f4872.Services;
using f4872.ViewModels;

namespace f4872.Controllers;

// El panel lo usa una persona, así que no hay usuarios ni roles: hay una clave
// y una cookie. Sin ASP.NET Identity, que traería tablas de usuarios, de roles
// y de reclamos para un solo vendedor que no va a tener compañeros.
[Authorize]
[Route("panel")]
public class PanelController : Controller
{
    private readonly Contexto _contexto;
    private readonly RecetaService _recetas;
    private readonly string? _clave;

    // Un segundo de espera cuando la clave está mal. No molesta al que se
    // equivoca una vez y le arruina el día al que quiere probar de a miles.
    private static readonly TimeSpan Castigo = TimeSpan.FromSeconds(1);

    public PanelController(Contexto contexto, RecetaService recetas, IConfiguration configuracion)
    {
        _contexto = contexto;
        _recetas = recetas;
        _clave = configuracion["Panel:Clave"];
    }

    public async Task<IActionResult> Index()
    {
        var marco = await Marco();
        var consolidado = await _recetas.Consolidar();

        return View(new InicioVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Cifras = await Cifras(),
            Hornear = Hornear(consolidado),
            Comprar = await _recetas.FaltaComprar()
        });
    }

    // Que cuesta hacer cada producto y que deja.
    //
    // La lista va por tipo, y adentro de cada tipo por margen de menor a mayor:
    // la pantalla es para ver que revisar, y una pizza se compara con otra
    // pizza, no con una empanada.
    [HttpGet("costos")]
    public async Task<IActionResult> Costos(int? producto = null, string vista = "producto")
    {
        var marco = await Marco();

        // Costos() ya viene del peor margen al mejor, y OrderBy es estable:
        // ese orden sigue igual adentro de cada tipo. Se ordena aca y no en la
        // vista para que el elegido de entrada sea el primer renglon que se ve
        var lista = (await _recetas.Costos()).OrderBy(x => x.Familia).ToList();
        var meses = vista == "mes" ? await _recetas.PorMes() : [];

        // la banda es lo unico de la pantalla que mira los pedidos: el resto es
        // por unidad y no cambia de una semana a la otra
        var pedidas = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Nuevo || x.Pedido.Estado == EstadoPedido.Preparando)
            .GroupBy(x => x.IdProducto)
            .Select(g => new { IdProducto = g.Key, Piezas = g.Sum(x => x.Cantidad * (x.UnidadesPorPack ?? 1)) })
            .ToDictionaryAsync(x => x.IdProducto, x => x.Piezas);

        var cuesta = lista.Sum(x => x.Costo * pedidas.GetValueOrDefault(x.IdProducto));
        var cobra = lista.Sum(x => x.Venta * pedidas.GetValueOrDefault(x.IdProducto));

        var incompletos = lista.Count(x => x.SinPrecio > 0);
        var flojos = lista.Count(x => x.Flojo);

        // el promedio sale solo de los cerrados: el mes en curso todavia se
        // mueve y arrastraria el numero para cualquier lado
        var cerrados = meses.Where(x => !x.Abierto && x.Porcentaje is not null).ToList();

        var queda = await _contexto.ItemPedidos
            .Where(x => x.Pedido.Estado == EstadoPedido.Nuevo || x.Pedido.Estado == EstadoPedido.Preparando)
            .SumAsync(x => (decimal?)(x.Cantidad * x.PrecioUnitario)) ?? 0m;

        return View(new CostosVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Vista = vista == "mes" ? "mes" : "producto",
            Lista = lista,
            Meses = meses,
            EnCurso = meses.FirstOrDefault(x => x.Abierto),
            SinEntregarPlata = queda > 0 ? queda.ToString("C") : null,
            PromedioCerrados = cerrados.Count > 0
                ? $"{Math.Round(cerrados.Average(x => (double)x.Porcentaje!.Value))}%"
                : null,
            Elegido = producto is int elegido
                ? lista.FirstOrDefault(x => x.IdProducto == elegido) ?? lista.FirstOrDefault()
                : lista.FirstOrDefault(),
            Cuesta = cuesta.ToString("C"),
            SeCobra = cobra.ToString("C"),
            Deja = (cobra - cuesta).ToString("C"),
            Margen = cobra > 0 ? $"{Math.Round((cobra - cuesta) / cobra * 100)}%" : null,
            // lo que falta cargar tapa al margen flojo: mientras haya un costo a
            // medias el porcentaje que se muestra no es el de verdad
            Aparte = incompletos > 0
                ? $"{incompletos} {(incompletos == 1 ? "producto" : "productos")} sin terminar de cargar"
                : flojos > 0
                    ? $"{flojos} {(flojos == 1 ? "producto" : "productos")} bajo el {CostosVm.MargenMinimo}%"
                    : $"ninguno bajo el {CostosVm.MargenMinimo}%"
        });
    }

    // Ingredientes: todos, con la lista de compras abierta en columnas.
    //
    // Stock, Necesito y Falta, en el orden de la resta. El stock se edita en el
    // renglon; las otras dos salen solas de los pedidos.
    [HttpGet("ingredientes")]
    public async Task<IActionResult> Ingredientes(int? ingrediente = null, bool nuevo = false)
    {
        var marco = await Marco();
        var necesita = await _recetas.Necesita();

        var ingredientes = await _contexto.Ingredientes
            .Select(x => new
            {
                x.IdIngrediente,
                x.Nombre,
                x.Stock,
                x.Libre,
                x.Unidad,
                // en cuantos productos y en cuantas recetas aparece. Las recetas
                // cuentan: la harina esta en el bollo y en ninguna pizza, y decir
                // «todavia en ninguna receta» al lado de «necesito 2 kg» seria falso
                EnProductos = x.UsosEnProductos.Count,
                Recetas = x.UsosEnRecetas.Select(u => u.Receta.Nombre).ToList()
            })
            .ToListAsync();

        // Todos, haya pedidos o no. Con solo los de los pedidos la pantalla
        // quedaba vacia toda la semana, que es cuando se carga la carta. El
        // sabado se sigue leyendo como lista de compras: lo que falta va arriba.
        var filas = ingredientes
            .Select(x =>
            {
                var cuanto = necesita.GetValueOrDefault(x.IdIngrediente);
                var falta = cuanto - x.Stock;

                return new FilaIngrediente
                {
                    IdIngrediente = x.IdIngrediente,
                    Nombre = x.Nombre,
                    // exacto: este es el que se edita y tiene que volver entero
                    Stock = Cantidades.Bonito(x.Stock, x.Unidad, exacto: true),
                    Necesito = cuanto > 0 ? Cantidades.Bonito(cuanto, x.Unidad) : "—",
                    Falta = falta > 0 ? Cantidades.Bonito(falta, x.Unidad) : "—",
                    HayQueComprar = !x.Libre && falta > 0,
                    Donde = Donde(x.EnProductos, x.Recetas)
                };
            })
            // primero lo que mas falta, y despues por nombre: es el orden en que
            // se hace una compra
            .OrderByDescending(x => x.HayQueComprar)
            .ThenBy(x => x.Nombre)
            .ToList();

        return View(new IngredientesVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Lista = filas,
            Pedidos = marco.SinEntregar,
            Faltantes = filas.Count(x => x.HayQueComprar),
            // en blanco es el alta: la misma ficha sin nada cargado
            Ficha = nuevo ? new FichaIngrediente()
                : ingrediente is int elegido ? await Ficha(elegido)
                : null,
            EnTotal = ingredientes.Count,
            EnRecetas = ingredientes.Count(x => x.EnProductos > 0 || x.Recetas.Count > 0)
        });
    }

    // La ficha de un ingrediente: como se llama, en que se mide y como se compra.
    //
    // Se arma aparte de la grilla porque son dos preguntas distintas -una es la
    // compra del finde y la otra es la ficha de una cosa- y porque la grilla se
    // dibuja igual con la ficha cerrada, que es el estado normal.
    private async Task<FichaIngrediente?> Ficha(int id)
    {
        var x = await _contexto.Ingredientes
            .Where(i => i.IdIngrediente == id)
            .Select(i => new
            {
                i.IdIngrediente,
                i.Nombre,
                i.Unidad,
                i.Libre,
                i.CantidadDeCompra,
                i.PrecioDeCompra,
                i.PrecioPorMedida,
                EnProductos = i.UsosEnProductos.Count,
                Recetas = i.UsosEnRecetas.Select(u => u.Receta.Nombre).ToList()
            })
            .FirstOrDefaultAsync();

        if (x is null)
        {
            return null;
        }

        var partes = await _recetas.Desglose(id);

        return new FichaIngrediente
        {
            IdIngrediente = x.IdIngrediente,
            Nombre = x.Nombre,
            Unidad = x.Unidad,
            Libre = x.Libre,
            // pelado: la unidad se dibuja al lado y no adentro del campo
            Bulto = x.CantidadDeCompra is decimal trae ? trae.ToString("0.###") : "",
            Precio = x.PrecioDeCompra,
            Titulo = x.Nombre,
            Donde = Donde(x.EnProductos, x.Recetas),
            Usos = x.EnProductos + x.Recetas.Count,
            Desglose = [.. partes.Select(p => Renglon(p, x.Unidad))],
            HaceFalta = Cantidades.Bonito(partes.Sum(p => p.Total), x.Unidad)
        };
    }

    // Como se lee un renglon del desglose.
    //
    // Una receta multiplica por cuantas veces hay que hacerla y no por piezas:
    // se hace entera, asi que lo que se gasta es lo que lleva toda por esas
    // veces. «1 kg la receta × 2 = 2 kg», con el rinde al lado del nombre.
    private static RenglonDesglose Renglon(ParteDeReceta p, Medida unidad) => new()
    {
        Donde = p.Donde,
        Rinde = p.EsReceta ? $"rinde {p.Rinde}" : null,
        Cuanto = p.EsReceta
            ? $"{Cantidades.Bonito(p.Cantidad, unidad)} la receta"
            : Cantidades.Bonito(p.Cantidad, unidad),
        Sale = $"× {(p.EsReceta ? p.Veces : p.Piezas)} = {Cantidades.Bonito(p.Total, unidad)}"
    };

    // Donde se usa un ingrediente. Las recetas van nombradas cuando es una sola:
    // «en bollo de pizza» dice mas que «en 1 receta». Con mas de una se cuentan,
    // para no armar un renglon largo.
    private static string Donde(int productos, IReadOnlyList<string> recetas)
    {
        var partes = new List<string>();

        if (productos > 0)
        {
            partes.Add($"en {productos} {(productos == 1 ? "producto" : "productos")}");
        }

        if (recetas.Count == 1)
        {
            partes.Add($"en {recetas[0].ToLowerInvariant()}");
        }
        else if (recetas.Count > 1)
        {
            partes.Add($"en {recetas.Count} recetas");
        }

        return partes.Count == 0 ? "todavía en ninguna receta" : string.Join(" y ", partes);
    }

    // Guardar la ficha: nombre, medida y como se compra.
    //
    // Va entera y de una vez -y no campo por campo como el stock- porque los
    // cuatro se leen juntos: cambiar la medida sin tocar el bulto deja «25 kg»
    // queriendo decir otra cosa.
    [HttpPost("ingredientes/guardar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarIngrediente(FichaIngrediente ficha)
    {
        var nombre = (ficha.Nombre ?? "").Trim();

        if (nombre.Length == 0)
        {
            return await VolverAFicha(ficha, "Escribí cómo se llama.");
        }

        // el nombre es unico en la tabla: mejor decirlo con palabras que dejar
        // que reviente el indice
        var repetido = await _contexto.Ingredientes
            .AnyAsync(x => x.IdIngrediente != ficha.IdIngrediente && x.Nombre.ToLower() == nombre.ToLower());

        if (repetido)
        {
            return await VolverAFicha(ficha, $"Ya hay un ingrediente que se llama «{nombre}».");
        }

        // Vacio quiere decir «todavia no se cuanto trae» y se guarda nulo. Lo
        // que no se entiende es distinto: alguien quiso escribir algo, y
        // borrarle el dato en silencio seria peor que rebotarlo.
        decimal? bulto = null;

        if (!string.IsNullOrWhiteSpace(ficha.Bulto))
        {
            bulto = Cantidades.Leer(ficha.Bulto, ficha.Unidad);

            if (bulto is null)
            {
                return await VolverAFicha(ficha,
                    $"No entiendo «{ficha.Bulto.Trim()}» como cantidad. Escribí solo el número.");
            }

            // cero se entiende, pero un bulto que no trae nada no es un bulto
            if (bulto == 0)
            {
                return await VolverAFicha(ficha,
                    "El bulto no puede ser cero: es cuánto trae la compra. Dejalo vacío si todavía no lo sabés.");
            }
        }

        var ingrediente = ficha.EsNuevo
            ? new Ingrediente()
            : await _contexto.Ingredientes.FindAsync(ficha.IdIngrediente)
                ?? throw new InvalidOperationException($"No existe el ingrediente {ficha.IdIngrediente}.");

        ingrediente.Nombre = nombre;
        ingrediente.Unidad = ficha.Unidad;
        // Libre no se toca: no viene del formulario, y el binder lo daria en
        // false por no estar. Guardar la ficha del agua la volveria comprable.
        ingrediente.CantidadDeCompra = bulto;
        ingrediente.PrecioDeCompra = ficha.Precio > 0 ? ficha.Precio : null;

        if (ficha.EsNuevo)
        {
            _contexto.Ingredientes.Add(ingrediente);
        }

        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Ingredientes));
    }

    // Borrar uno que este en alguna receta la dejaria rota, y las dos claves
    // foraneas son ON DELETE RESTRICT. Se cuenta antes para decirlo con palabras
    // y no reventar contra la base; el enlace ni siquiera se dibuja cuando tiene
    // usos, asi que llegar aca con alguno es que la receta cambio mientras tanto.
    [HttpPost("ingredientes/borrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BorrarIngrediente(int id)
    {
        var ingrediente = await _contexto.Ingredientes
            .Include(x => x.UsosEnProductos)
            .Include(x => x.UsosEnRecetas)
            .FirstOrDefaultAsync(x => x.IdIngrediente == id)
            ?? throw new InvalidOperationException($"No existe el ingrediente {id}.");

        var usos = ingrediente.UsosEnProductos.Count + ingrediente.UsosEnRecetas.Count;

        if (usos > 0)
        {
            throw new InvalidOperationException(
                $"«{ingrediente.Nombre}» está en {usos} recetas: hay que sacarlo de ahí antes de borrarlo.");
        }

        _contexto.Ingredientes.Remove(ingrediente);
        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Ingredientes));
    }

    // Vuelve a dibujar la pantalla con la ficha abierta y el error puesto,
    // conservando lo tipeado. El encabezado sigue saliendo del registro
    // guardado: es la identidad de lo que estas editando, no lo que escribiste.
    private async Task<IActionResult> VolverAFicha(FichaIngrediente ficha, string error)
    {
        var vm = (IngredientesVm)((ViewResult)await Ingredientes(
            ficha.EsNuevo ? null : ficha.IdIngrediente, ficha.EsNuevo)).Model!;

        // en un alta no hay registro guardado del que sacarlos: la ficha es todo
        // lo que hay
        if (vm.Ficha is not null && !ficha.EsNuevo)
        {
            ficha.Titulo = vm.Ficha.Titulo;
            ficha.Donde = vm.Ficha.Donde;
            ficha.Libre = vm.Ficha.Libre;
            ficha.Usos = vm.Ficha.Usos;
            ficha.Desglose = vm.Ficha.Desglose;
            ficha.HaceFalta = vm.Ficha.HaceFalta;
        }

        vm.Ficha = ficha;
        vm.Error = error;

        // con el nombre puesto: sin el, MVC busca la vista de la accion que se
        // esta ejecutando -GuardarIngrediente- y no la que arma la pantalla
        return View(nameof(Ingredientes), vm);
    }

    // El stock se edita en el renglon y se guarda solo ese renglon: son
    // treinta y siete, y mandar la tabla entera para cambiar un numero seria
    // pisar lo que otro pudo haber tocado mientras tanto.
    [HttpPost("ingredientes/stock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stock(int id, string? stock)
    {
        var ingrediente = await _contexto.Ingredientes.FindAsync(id)
            ?? throw new InvalidOperationException($"No existe el ingrediente {id}.");

        // Texto y no decimal porque el campo viene con la unidad escrita y hay
        // que leerla para saber si «2» son dos gramos o dos kilos. Lo que no se
        // entiende vuelve nulo y el stock queda como estaba: el cero si es un
        // valor -quedarse sin algo es un estado normal- pero un campo vacio no.
        if (Cantidades.Leer(stock, ingrediente.Unidad) is decimal leido)
        {
            ingrediente.Stock = leido;
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Ingredientes));
    }

    // Productos: la lista a la izquierda y la ficha a la derecha.
    //
    // No hay pantalla de alta distinta de la de edicion: con ?nuevo=1 se dibuja
    // la misma ficha en blanco.
    [HttpGet("productos")]
    public async Task<IActionResult> Productos(string? familia = null, int? producto = null,
        bool nuevo = false, int? sumando = null)
    {
        return View(await Catalogo(familia, producto, nuevo, sumando));
    }

    // Guardar la ficha, de alta o de edicion. Es un solo camino porque es una
    // sola pantalla; lo unico que cambia es si hay id.
    [HttpPost("productos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarProducto(FichaProducto ficha, string? familia = null, bool nuevo = false)
    {
        var nombre = (ficha.Nombre ?? "").Trim();

        if (nombre.Length < 2)
        {
            return await Volver(familia, ficha, nuevo, "Falta el nombre del producto.");
        }

        // Las empanadas no tienen precio propio: se cobran por pack. Guardar un
        // numero ahi seria guardar un precio que nadie cobra.
        var precio = ficha.Familia == Familia.Empanada ? null : ficha.Precio;

        if (precio is null && ficha.Familia != Familia.Empanada)
        {
            return await Volver(familia, ficha, nuevo, "Falta el precio.");
        }

        if (precio <= 0)
        {
            return await Volver(familia, ficha, nuevo, "El precio tiene que ser mayor que cero.");
        }

        var producto = nuevo
            ? new Producto()
            : await _contexto.Productos.FindAsync(ficha.IdProducto)
                ?? throw new InvalidOperationException($"No existe el producto {ficha.IdProducto}.");

        // antes de pisarla, para saber si se mudo de familia
        var cambioDeFamilia = !nuevo && producto.Familia != ficha.Familia;

        producto.Nombre = nombre;
        producto.Familia = ficha.Familia;
        producto.Precio = precio;

        // La masa va con la familia: una pizza amasa el bollo de pizza, sea
        // nueva o recien mudada. Sin esto una pizza nueva quedaba sin base y
        // salia $219 mas barata, sin entrar en el amasado ni en la harina que
        // hay que comprar -y todos los numeros seguian pareciendo creibles.
        if (nuevo || cambioDeFamilia)
        {
            producto.IdBase = await BaseDe(ficha.Familia);
        }

        // La salsa se elige, y es de las pizzas y las focaccias: una empanada
        // lleva relleno. Que exista y que sea una salsa se mira acá: la clave
        // foránea solo sabe que es una receta, y un relleno también lo es.
        int? salsa = null;

        if (ficha.Familia != Familia.Empanada && ficha.IdSalsa is int idSalsa)
        {
            if (!await _contexto.Recetas.AnyAsync(x => x.IdReceta == idSalsa && x.Tipo == TipoReceta.Salsa))
            {
                return await Volver(familia, ficha, nuevo, "Esa salsa ya no está en Recetas: elegí otra.");
            }

            salsa = idSalsa;
        }

        producto.IdSalsa = salsa;

        // El relleno es de las empanadas y es todo lo que llevan, así que una
        // nueva no se crea sin él. A una que ya existe se la deja guardar sin
        // relleno: los precios de los packs se cargan desde la ficha de
        // cualquier gusto, y no pueden quedar trabados por el de uno.
        int? relleno = null;

        if (ficha.Familia == Familia.Empanada)
        {
            if (ficha.IdRelleno is int idRelleno)
            {
                if (!await _contexto.Recetas.AnyAsync(x => x.IdReceta == idRelleno && x.Tipo == TipoReceta.Relleno))
                {
                    return await Volver(familia, ficha, nuevo, "Ese relleno ya no está en Recetas: elegí otro.");
                }

                relleno = idRelleno;
            }
            else if (nuevo)
            {
                return await Volver(familia, ficha, nuevo, "Falta elegir el relleno.");
            }
        }

        producto.IdRelleno = relleno;

        if (nuevo)
        {
            _contexto.Productos.Add(producto);
        }

        // Las cantidades de la receta viajan con la ficha: se editan en el
        // renglon y se guardan con el mismo boton que el nombre y el precio.
        foreach (var renglon in ficha.Receta ?? [])
        {
            if (renglon.Cantidad <= 0)
            {
                return await Volver(familia, ficha, nuevo,
                    $"La cantidad de {renglon.Nombre.ToLowerInvariant()} tiene que ser mayor que cero.");
            }

            var fila = await _contexto.ProductoIngredientes
                .FirstOrDefaultAsync(x => x.IdProducto == ficha.IdProducto && x.IdIngrediente == renglon.IdIngrediente);

            if (fila is not null)
            {
                fila.Cantidad = renglon.Cantidad;
            }
        }

        // Los dos precios de pack valen para todos los gustos, asi que se
        // guardan desde la ficha de cualquiera. No son de este producto.
        foreach (var pack in ficha.Packs ?? [])
        {
            var fila = await _contexto.Packs.FirstOrDefaultAsync(x => x.Unidades == pack.Unidades);
            if (fila is not null && pack.Precio > 0)
            {
                fila.Precio = pack.Precio;
            }
        }

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            // el indice unico es (Familia, Nombre): dos productos con el mismo
            // nombre en la misma familia son el mismo producto
            return await Volver(familia, ficha, nuevo, $"Ya hay un/a {ficha.Familia.ToString().ToLowerInvariant()} que se llama «{nombre}».");
        }

        return RedirectToAction(nameof(Productos), new { familia, producto = producto.IdProducto });
    }

    // Sumar un ingrediente a la receta.
    //
    // Solo se puede sumar uno que ya exista. Escribir libre es como entran
    // «Oregano» y «Oregano» con acento a la base como dos ingredientes
    // distintos, y despues la lista de compras los cuenta por separado.
    [HttpPost("productos/receta/sumar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SumarIngrediente(int id, string? nombre, decimal? cantidad, string? familia = null)
    {
        var buscado = (nombre ?? "").Trim();

        var ingrediente = await _contexto.Ingredientes
            .FirstOrDefaultAsync(x => x.Nombre.ToLower() == buscado.ToLower());

        if (ingrediente is null)
        {
            return await Volver(familia, id, buscado.Length == 0
                ? "Escribí el nombre del ingrediente."
                : $"No hay ningún ingrediente que se llame «{buscado}». Se dan de alta en Ingredientes.");
        }

        // Sin cantidad no es un error: es el paso del medio. El renglon de
        // arriba vuelve a dibujarse con el ingrediente ya elegido y el campo de
        // cuanto esperando, que es como se carga con JavaScript en un solo
        // viaje y sin el en dos. La tabla exige mayor que cero, asi que la
        // cantidad no se puede dejar para despues.
        if (cantidad is not > 0)
        {
            return RedirectToAction(nameof(Productos),
                new { familia, producto = id, sumando = ingrediente.IdIngrediente });
        }

        var fila = await _contexto.ProductoIngredientes
            .FirstOrDefaultAsync(x => x.IdProducto == id && x.IdIngrediente == ingrediente.IdIngrediente);

        if (fila is null)
        {
            _contexto.ProductoIngredientes.Add(new ProductoIngrediente
            {
                IdProducto = id,
                IdIngrediente = ingrediente.IdIngrediente,
                Cantidad = cantidad.Value
            });
        }
        else
        {
            // ya estaba: sumarlo de nuevo corrige la cantidad en vez de rebotar
            fila.Cantidad = cantidad.Value;
        }

        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Productos), new { familia, producto = id });
    }

    // Si el cliente puede pedir la pizza sin esto. Es del par producto-
    // ingrediente y no del ingrediente: la muzzarella se saca de una fugazzeta
    // y de una napolitana no.
    [HttpPost("productos/receta/modificable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Modificable(int id, int idIngrediente, string? familia = null)
    {
        var fila = await _contexto.ProductoIngredientes
            .FirstOrDefaultAsync(x => x.IdProducto == id && x.IdIngrediente == idIngrediente);

        if (fila is not null)
        {
            fila.Quitable = !fila.Quitable;
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Productos), new { familia, producto = id });
    }

    [HttpPost("productos/receta/quitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarIngrediente(int id, int idIngrediente, string? familia = null)
    {
        var fila = await _contexto.ProductoIngredientes
            .FirstOrDefaultAsync(x => x.IdProducto == id && x.IdIngrediente == idIngrediente);

        if (fila is not null)
        {
            _contexto.ProductoIngredientes.Remove(fila);
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Productos), new { familia, producto = id });
    }

    // Marcar agotado o devolverlo a la carta.
    //
    // No hay boton de borrar: un producto puede estar nombrado en pedidos
    // viejos, y borrarlo dejaria el historial hablando de algo que no existe.
    [HttpPost("productos/agotar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Agotar(int id, string? familia = null)
    {
        var producto = await _contexto.Productos.FindAsync(id)
            ?? throw new InvalidOperationException($"No existe el producto {id}.");

        producto.Activo = !producto.Activo;
        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Productos), new { familia, producto = id });
    }

    // el error de la receta no tiene nada tipeado que conservar: alcanza con
    // volver a la ficha del producto y decir que pasó
    private async Task<IActionResult> Volver(string? familia, int producto, string error)
    {
        var vm = await Catalogo(familia, producto, false);
        vm.Error = error;
        return View(nameof(Productos), vm);
    }

    // vuelve a dibujar la pantalla con lo tipeado y el motivo, en vez de perderlo
    private async Task<IActionResult> Volver(string? familia, FichaProducto ficha, bool nuevo, string error)
    {
        var vm = await Catalogo(familia, nuevo ? null : ficha.IdProducto, nuevo);
        // lo que no se escribe no viaja en el formulario: sale de lo guardado
        ficha.Packs = vm.Ficha.Packs;
        ficha.Base = vm.Ficha.Base;
        ficha.Salsas = vm.Ficha.Salsas;
        ficha.Rellenos = vm.Ficha.Rellenos;
        vm.Ficha = ficha;
        vm.EsNuevo = nuevo;
        vm.Error = error;
        return View(nameof(Productos), vm);
    }

    // Que masa amasa una familia. Lo dice la masa y no los productos que ya la
    // usan: en una base recien puesta en marcha no hay ninguno, y la primera
    // pizza quedaba sin masa. Nulo en empanadas, que no amasan: no tienen fila.
    private async Task<int?> BaseDe(Familia familia) =>
        await _contexto.Recetas
            .Where(x => x.Tipo == TipoReceta.Base && x.Familia == familia)
            .Select(x => (int?)x.IdReceta)
            .FirstOrDefaultAsync();

    // Lo que le toca a una pieza de cada receta, para leer en la ficha del
    // producto: la base y las salsas.
    //
    // La receta esta cargada entera y aca se divide por el rinde, que es lo que
    // la pone en la misma unidad que el resto de la ficha: todo lo demas de esa
    // pantalla es por pieza.
    private async Task<List<RecetaPorPieza>> PorPieza(IQueryable<Receta> recetas)
    {
        var lista = await recetas
            .Where(x => x.Rinde > 0)
            .OrderBy(x => x.IdReceta)
            .Select(x => new
            {
                x.IdReceta,
                x.Nombre,
                x.Rinde,
                Renglones = x.Ingredientes
                    .OrderByDescending(r => r.Cantidad)
                    .Select(r => new { r.Ingrediente.Nombre, r.Ingrediente.Unidad, r.Cantidad })
                    .ToList()
            })
            .ToListAsync();

        return
        [
            .. lista.Select(x => new RecetaPorPieza
            {
                IdReceta = x.IdReceta,
                Nombre = x.Nombre,
                Renglones =
                [
                    .. x.Renglones.Select(r => new RenglonPorPieza
                    {
                        Nombre = r.Nombre,
                        Cuanto = Cantidades.Bonito(r.Cantidad / x.Rinde, r.Unidad)
                    })
                ]
            })
        ];
    }

    private async Task<ProductosVm> Catalogo(string? familia, int? producto, bool nuevo, int? sumando = null)
    {
        familia = ProductosVm.Chips.Any(x => x.Clave == familia) ? familia! : "todo";

        var todos = _contexto.Productos.AsQueryable();
        if (familia != "todo" && Enum.TryParse<Familia>(familia, out var cual))
        {
            todos = todos.Where(x => x.Familia == cual);
        }

        var lista = await todos
            .Select(x => new FilaProducto
            {
                IdProducto = x.IdProducto,
                Nombre = x.Nombre,
                Familia = x.Familia,
                Precio = x.Precio,
                Activo = x.Activo
            })
            .ToListAsync();

        // Familia se guarda como texto: ordenar en la base saldria alfabetico
        lista = [.. lista.OrderBy(x => x.Familia).ThenBy(x => x.IdProducto)];

        var packs = await _contexto.Packs
            .OrderBy(x => x.Unidades)
            .Select(x => new PrecioPack { Unidades = x.Unidades, Precio = x.Precio })
            .ToListAsync();

        var elegido = nuevo
            ? null
            : lista.FirstOrDefault(x => x.IdProducto == producto) ?? lista.FirstOrDefault();

        // La receta de este producto, y los que todavia no estan. Sin columna de
        // orden, van por nombre: es el orden en que se busca uno en una lista.
        var receta = elegido is null
            ? []
            : await _contexto.ProductoIngredientes
                .Where(x => x.IdProducto == elegido.IdProducto)
                .OrderBy(x => x.Ingrediente.Nombre)
                .Select(x => new IngredienteDeLaReceta
                {
                    IdIngrediente = x.IdIngrediente,
                    Nombre = x.Ingrediente.Nombre,
                    Cantidad = x.Cantidad,
                    Unidad = Cantidades.Abreviatura(x.Ingrediente.Unidad),
                    Modificable = x.Quitable
                })
                .ToListAsync();

        // La masa que amasa, la salsa que lleva y, si es una empanada, su
        // relleno. Nulos en un alta.
        var usa = elegido is null
            ? null
            : await _contexto.Productos
                .Where(x => x.IdProducto == elegido.IdProducto)
                .Select(x => new { x.IdBase, x.IdSalsa, x.IdRelleno })
                .FirstOrDefaultAsync();

        var laBase = usa?.IdBase is int idBase
            ? (await PorPieza(_contexto.Recetas.Where(x => x.IdReceta == idBase))).FirstOrDefault()
            : null;

        // Todas las salsas y no solo la elegida: al tocar otra, la ficha abre lo
        // que lleva sin esperar a guardar.
        var salsas = await PorPieza(_contexto.Recetas.Where(x => x.Tipo == TipoReceta.Salsa));
        var rellenos = await PorPieza(_contexto.Recetas.Where(x => x.Tipo == TipoReceta.Relleno));

        // el que se eligio y esta esperando la cantidad, si hay alguno
        var enEspera = sumando is null
            ? null
            : await _contexto.Ingredientes
                .Where(x => x.IdIngrediente == sumando)
                .Select(x => new IngredienteDeLaReceta
                {
                    IdIngrediente = x.IdIngrediente,
                    Nombre = x.Nombre,
                    Unidad = Cantidades.Abreviatura(x.Unidad)
                })
                .FirstOrDefaultAsync();

        var puestos = receta.Select(x => x.IdIngrediente).ToList();
        var disponibles = elegido is null
            ? []
            : await _contexto.Ingredientes
                .Where(x => !puestos.Contains(x.IdIngrediente))
                .OrderBy(x => x.Nombre)
                .Select(x => new { x.Nombre, x.Unidad })
                .ToListAsync();

        var agotados = lista.Count(x => !x.Activo);
        var marco = await Marco();

        return new ProductosVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Familia = familia,
            Lista = lista,
            EsNuevo = nuevo || elegido is null,
            Resumen = $"{lista.Count} · {(agotados == 0 ? "ninguno agotado" : agotados == 1 ? "1 agotado" : $"{agotados} agotados")}",
            Titulo = elegido?.Nombre ?? "",
            Subtitulo = elegido is null ? "" : elegido.Familia switch
            {
                Familia.Pizza => "Pizza",
                Familia.Focaccia => "Focaccia",
                _ => "Empanada"
            },
            ActivoGuardado = elegido?.Activo ?? true,
            Ficha = elegido is null
                ? new FichaProducto { Packs = packs, Salsas = salsas, Rellenos = rellenos }
                : new FichaProducto
                {
                    IdProducto = elegido.IdProducto,
                    Nombre = elegido.Nombre,
                    Familia = elegido.Familia,
                    Precio = elegido.Precio,
                    Activo = elegido.Activo,
                    Packs = packs,
                    Receta = receta,
                    Base = laBase,
                    IdSalsa = usa?.IdSalsa,
                    Salsas = salsas,
                    IdRelleno = usa?.IdRelleno,
                    Rellenos = rellenos,
                    Disponibles = [.. disponibles.Select(x => new IngredienteDisponible
                    {
                        Nombre = x.Nombre,
                        Unidad = Cantidades.Abreviatura(x.Unidad)
                    })],
                    Sumando = enEspera
                }
        };
    }

    // Recetas: lo que se prepara aparte y usan varios productos. Las bases, las
    // salsas y los rellenos, cada una con lo que lleva.
    //
    // Se carga entera, como se hace, y la pantalla divide: al lado de cada
    // cantidad va lo que le toca a una pieza. Como en Productos, no hay pantalla
    // de alta distinta: con ?nueva=true es la misma ficha en blanco.
    [HttpGet("recetas")]
    public async Task<IActionResult> Recetas(string? tipo = null, int? receta = null,
        bool nueva = false, int? sumando = null)
    {
        return View(await Recetario(tipo, receta, nueva, sumando));
    }

    // Guardar la ficha, de alta o de edición: el nombre, el tipo, el rinde y
    // las cantidades, todo con el mismo botón.
    [HttpPost("recetas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarReceta(FichaReceta ficha, string? tipo = null, bool nueva = false)
    {
        var nombre = (ficha.Nombre ?? "").Trim();

        if (nombre.Length < 2)
        {
            return await VolverAReceta(tipo, ficha, nueva, "Falta el nombre de la receta.");
        }

        // toda la cuenta de una receta es dividir por el rinde, y en piezas: no
        // hay media pizza
        if (ficha.Rinde <= 0)
        {
            return await VolverAReceta(tipo, ficha, nueva, "El rinde tiene que ser un número entero mayor que cero.");
        }

        var receta = nueva
            ? new Receta()
            : await _contexto.Recetas.FindAsync(ficha.IdReceta)
                ?? throw new InvalidOperationException($"No existe la receta {ficha.IdReceta}.");

        // Las bases no se crean ni cambian de tipo: hay una por familia y cada
        // pizza o focaccia toma la suya sola. Una receta nueva es una salsa o un
        // relleno, diga lo que diga el formulario.
        var nuevoTipo = !nueva && receta.Tipo == TipoReceta.Base ? TipoReceta.Base
            : ficha.Tipo == TipoReceta.Relleno ? TipoReceta.Relleno
            : TipoReceta.Salsa;

        // Una que usa algún producto no cambia de tipo: la pizza se quedaría con
        // un relleno como salsa. Primero hay que sacársela.
        if (!nueva && nuevoTipo != receta.Tipo)
        {
            var usan = await UsanLaReceta(receta.IdReceta);

            if (usan.Count > 0)
            {
                return await VolverAReceta(tipo, ficha, nueva,
                    $"«{receta.Nombre}» no puede dejar de ser {receta.Tipo.ToString().ToLowerInvariant()}: " +
                    $"la {(usan.Count == 1 ? "usa" : "usan")} {EnLista(usan)}. " +
                    $"Primero cambiales {(receta.Tipo == TipoReceta.Salsa ? "la salsa" : "el relleno")}.");
            }
        }

        receta.Nombre = nombre;
        receta.Tipo = nuevoTipo;
        receta.Rinde = ficha.Rinde;

        if (nueva)
        {
            _contexto.Recetas.Add(receta);
        }

        // las cantidades viajan con la ficha, igual que en la de un producto
        foreach (var renglon in ficha.Ingredientes ?? [])
        {
            if (renglon.Cantidad <= 0)
            {
                return await VolverAReceta(tipo, ficha, nueva,
                    $"La cantidad de {renglon.Nombre.ToLowerInvariant()} tiene que ser mayor que cero.");
            }

            var fila = await _contexto.RecetaIngredientes
                .FirstOrDefaultAsync(x => x.IdReceta == ficha.IdReceta && x.IdIngrediente == renglon.IdIngrediente);

            if (fila is not null)
            {
                fila.Cantidad = renglon.Cantidad;
            }
        }

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            // el nombre es único entre todas, de cualquier tipo
            return await VolverAReceta(tipo, ficha, nueva, $"Ya hay una receta que se llama «{nombre}».");
        }

        // si el chip de arriba la dejaba afuera -una salsa nueva con «Rellenos»
        // encendido-, se pasa al de su tipo: si no, la que se acaba de guardar
        // no aparecería en la lista
        var filtro = tipo is null or "todo" || tipo == receta.Tipo.ToString() ? tipo : receta.Tipo.ToString();

        return RedirectToAction(nameof(Recetas), new { tipo = filtro, receta = receta.IdReceta });
    }

    // Sumar un ingrediente a la receta. Es el mismo camino que en la ficha de un
    // producto, en dos pasos: primero el nombre, que tiene que existir, y
    // después cuánto lleva.
    [HttpPost("recetas/sumar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SumarAReceta(int id, string? nombre, decimal? cantidad, string? tipo = null)
    {
        var buscado = (nombre ?? "").Trim();

        var ingrediente = await _contexto.Ingredientes
            .FirstOrDefaultAsync(x => x.Nombre.ToLower() == buscado.ToLower());

        if (ingrediente is null)
        {
            return await VolverAReceta(tipo, id, buscado.Length == 0
                ? "Escribí el nombre del ingrediente."
                : $"No hay ningún ingrediente que se llame «{buscado}». Se dan de alta en Ingredientes.");
        }

        if (cantidad is not > 0)
        {
            return RedirectToAction(nameof(Recetas),
                new { tipo, receta = id, sumando = ingrediente.IdIngrediente });
        }

        var fila = await _contexto.RecetaIngredientes
            .FirstOrDefaultAsync(x => x.IdReceta == id && x.IdIngrediente == ingrediente.IdIngrediente);

        if (fila is null)
        {
            _contexto.RecetaIngredientes.Add(new RecetaIngrediente
            {
                IdReceta = id,
                IdIngrediente = ingrediente.IdIngrediente,
                Cantidad = cantidad.Value
            });
        }
        else
        {
            // ya estaba: sumarlo de nuevo corrige la cantidad en vez de rebotar
            fila.Cantidad = cantidad.Value;
        }

        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Recetas), new { tipo, receta = id });
    }

    [HttpPost("recetas/quitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarDeReceta(int id, int idIngrediente, string? tipo = null)
    {
        var fila = await _contexto.RecetaIngredientes
            .FirstOrDefaultAsync(x => x.IdReceta == id && x.IdIngrediente == idIngrediente);

        if (fila is not null)
        {
            _contexto.RecetaIngredientes.Remove(fila);
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Recetas), new { tipo, receta = id });
    }

    // Borrar una receta que no usa nadie. Las bases no se borran: sin la masa,
    // la próxima pizza que se cargue queda sin bollo. El enlace no se dibuja en
    // esos casos, así que llegar acá con una es que algo cambió mientras tanto.
    [HttpPost("recetas/borrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BorrarReceta(int id, string? tipo = null)
    {
        var receta = await _contexto.Recetas.FindAsync(id)
            ?? throw new InvalidOperationException($"No existe la receta {id}.");

        if (receta.Tipo == TipoReceta.Base)
        {
            throw new InvalidOperationException($"«{receta.Nombre}» es una masa: las masas no se borran.");
        }

        var usan = await UsanLaReceta(id);

        if (usan.Count > 0)
        {
            throw new InvalidOperationException(
                $"«{receta.Nombre}» la {(usan.Count == 1 ? "usa" : "usan")} {EnLista(usan)}: " +
                "hay que sacársela antes de borrarla.");
        }

        // sus ingredientes se van con ella: la cascada está en la base
        _contexto.Recetas.Remove(receta);
        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Recetas), new { tipo });
    }

    // el error de sumar no tiene nada tipeado que conservar: alcanza con volver
    // a la ficha y decir qué pasó
    private async Task<IActionResult> VolverAReceta(string? tipo, int receta, string error)
    {
        var vm = await Recetario(tipo, receta, false);
        vm.Error = error;
        return View(nameof(Recetas), vm);
    }

    // Vuelve a dibujar la ficha con lo tipeado y el motivo. De lo guardado se
    // queda con lo que no se escribe -la unidad, lo de una pieza, lo que
    // cuesta- y a cada renglón le pone la cantidad que vino del formulario.
    private async Task<IActionResult> VolverAReceta(string? tipo, FichaReceta ficha, bool nueva, string error)
    {
        var vm = await Recetario(tipo, nueva ? null : ficha.IdReceta, nueva);

        var tipeadas = (ficha.Ingredientes ?? [])
            .GroupBy(x => x.IdIngrediente)
            .ToDictionary(g => g.Key, g => g.Last().Cantidad);

        foreach (var r in vm.Ficha.Ingredientes)
        {
            if (tipeadas.TryGetValue(r.IdIngrediente, out var cantidad))
            {
                r.Cantidad = cantidad;
            }
        }

        vm.Ficha.Nombre = ficha.Nombre ?? "";
        vm.Ficha.Rinde = ficha.Rinde;

        // una base sigue siendo base aunque el formulario diga otra cosa
        if (!vm.Ficha.EsBase)
        {
            vm.Ficha.Tipo = ficha.Tipo;
        }

        vm.Error = error;

        // con el nombre puesto: sin el, MVC busca la vista de la accion que se
        // esta ejecutando y no la que arma la pantalla
        return View(nameof(Recetas), vm);
    }

    // Los productos que usan una receta, en el orden en que se cargaron. Con
    // alguno, la receta no se borra ni cambia de tipo.
    private async Task<List<string>> UsanLaReceta(int id) =>
        await _contexto.Productos
            .Where(x => x.IdBase == id || x.IdSalsa == id || x.IdRelleno == id)
            .OrderBy(x => x.IdProducto)
            .Select(x => x.Nombre)
            .ToListAsync();

    // Quiénes la usan, en una línea: «Salsa · la usan Margarita y Marinara».
    // La base no nombra a cada pizza: la usan todas las de su familia, y la
    // lista entera sería un renglón de siete nombres.
    private static string Subtitulo(TipoReceta tipo, Familia? familia, IReadOnlyList<string> usan)
    {
        if (tipo == TipoReceta.Base)
        {
            var cuales = ProductosVm.Chips.FirstOrDefault(x => x.Clave == familia?.ToString()).Nombre?.ToLowerInvariant();

            return "Base · " + (usan.Count > 1 ? $"la usan las {usan.Count} {cuales}"
                : usan.Count == 1 ? $"la usa {usan[0]}"
                : $"todavía no hay {cuales}");
        }

        return (tipo == TipoReceta.Salsa ? "Salsa · " : "Relleno · ") + (usan.Count == 0
            ? "todavía no la usa ningún producto"
            : $"la {(usan.Count == 1 ? "usa" : "usan")} {EnLista(usan)}");
    }

    // «Margarita, Marinara y Napolitana»
    private static string EnLista(IReadOnlyList<string> nombres) =>
        nombres.Count < 2
            ? string.Join("", nombres)
            : string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres[^1];

    private async Task<RecetasVm> Recetario(string? tipo, int? receta, bool nueva, int? sumando = null)
    {
        tipo = RecetasVm.Chips.Any(x => x.Clave == tipo) ? tipo! : "todo";

        var todas = _contexto.Recetas.AsQueryable();
        if (tipo != "todo" && Enum.TryParse<TipoReceta>(tipo, out var cual))
        {
            todas = todas.Where(x => x.Tipo == cual);
        }

        var recetas = await todas
            .Select(x => new { x.IdReceta, x.Nombre, x.Tipo, x.Familia, x.Rinde })
            .ToListAsync();

        // Tipo se guarda como texto: ordenar en la base saldria alfabetico, y
        // las bases van primero. Adentro de cada tipo, en el orden de carga.
        recetas = [.. recetas.OrderBy(x => x.Tipo).ThenBy(x => x.IdReceta)];

        var costos = await _recetas.CostoDeRecetas();

        var lista = recetas
            .Select(x =>
            {
                var costo = costos.GetValueOrDefault(x.IdReceta);

                return new FilaReceta
                {
                    IdReceta = x.IdReceta,
                    Nombre = x.Nombre,
                    Tipo = x.Tipo,
                    CadaUna = costo is null ? null : $"{costo.PorPieza.ToString("C")} cada {RecetasVm.Pieza(x.Tipo)}",
                    SinPrecio = costo?.SinPrecio.Count ?? 0
                };
            })
            .ToList();

        var elegida = nueva
            ? null
            : recetas.FirstOrDefault(x => x.IdReceta == receta) ?? recetas.FirstOrDefault();

        var marco = await Marco();
        var vm = new RecetasVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Tipo = tipo,
            Lista = lista,
            EsNueva = elegida is null
        };

        if (elegida is null)
        {
            // El alta arranca con el tipo del chip, si es uno que se puede
            // crear, y con el rinde de siempre: una salsa rinde 6 pizzas, como
            // el bollo, y un relleno 12 empanadas.
            var alta = tipo == nameof(TipoReceta.Relleno) ? TipoReceta.Relleno : TipoReceta.Salsa;
            vm.Ficha = new FichaReceta { Tipo = alta, Rinde = alta == TipoReceta.Relleno ? 12 : 6 };
            return vm;
        }

        // sin columna de orden, van por nombre, como en la ficha del producto
        var renglones = await _contexto.RecetaIngredientes
            .Where(x => x.IdReceta == elegida.IdReceta)
            .OrderBy(x => x.Ingrediente.Nombre)
            .Select(x => new { x.IdIngrediente, x.Ingrediente.Nombre, x.Cantidad, x.Ingrediente.Unidad })
            .ToListAsync();

        // el que se eligio y esta esperando la cantidad, si hay alguno
        var enEspera = sumando is null
            ? null
            : await _contexto.Ingredientes
                .Where(x => x.IdIngrediente == sumando)
                .Select(x => new IngredienteDeLaReceta
                {
                    IdIngrediente = x.IdIngrediente,
                    Nombre = x.Nombre,
                    Unidad = Cantidades.Abreviatura(x.Unidad)
                })
                .FirstOrDefaultAsync();

        var puestos = renglones.Select(x => x.IdIngrediente).ToList();
        var disponibles = await _contexto.Ingredientes
            .Where(x => !puestos.Contains(x.IdIngrediente))
            .OrderBy(x => x.Nombre)
            .Select(x => new { x.Nombre, x.Unidad })
            .ToListAsync();

        var usan = await UsanLaReceta(elegida.IdReceta);
        var cuesta = costos.GetValueOrDefault(elegida.IdReceta);

        vm.Titulo = elegida.Nombre;
        vm.Subtitulo = Subtitulo(elegida.Tipo, elegida.Familia, usan);
        vm.Ficha = new FichaReceta
        {
            IdReceta = elegida.IdReceta,
            Nombre = elegida.Nombre,
            Tipo = elegida.Tipo,
            Rinde = elegida.Rinde,
            Ingredientes =
            [
                .. renglones.Select(r => new RenglonDeReceta
                {
                    IdIngrediente = r.IdIngrediente,
                    Nombre = r.Nombre,
                    Cantidad = r.Cantidad,
                    Unidad = Cantidades.Abreviatura(r.Unidad),
                    PorPieza = Cantidades.Bonito(r.Cantidad / elegida.Rinde, r.Unidad)
                })
            ],
            Disponibles = [.. disponibles.Select(x => new IngredienteDisponible
            {
                Nombre = x.Nombre,
                Unidad = Cantidades.Abreviatura(x.Unidad)
            })],
            Sumando = enEspera,
            Cuesta = cuesta is null
                ? null
                : $"Hacerla cuesta {cuesta.Entera.ToString("C")}: {cuesta.PorPieza.ToString("C")} cada {RecetasVm.Pieza(elegida.Tipo)}.",
            Falta = cuesta is null || cuesta.SinPrecio.Count == 0
                ? null
                : $"Falta el precio de {EnLista(cuesta.SinPrecio)}, así que es más.",
            Usos = usan.Count
        };

        return vm;
    }

    // Produccion: lo mismo que Hornear pero con el desglose abierto. Es la
    // pantalla del sabado a la manana -que hay que hacer, todo junto- y por eso
    // los «sin» van desglosados: no alcanza con el total cuando dos de esas
    // cinco margaritas se arman distinto.
    [HttpGet("produccion")]
    public async Task<IActionResult> Produccion()
    {
        var marco = await Marco();
        var consolidado = await _recetas.Consolidar();

        var masVieja = await _contexto.Pedidos
            .Where(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando)
            .OrderBy(x => x.FechaPedido)
            .Select(x => (DateTime?)x.FechaPedido)
            .FirstOrDefaultAsync();

        (string Nombre, Familia Familia)[] familias =
        [
            ("Pizzas", Familia.Pizza),
            ("Focaccias", Familia.Focaccia),
            ("Empanadas", Familia.Empanada)
        ];

        return View(new ProduccionVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Titulares =
            [
                new Titular
                {
                    Titulo = "Producción",
                    Valor = $"{consolidado.Bollos} bollos",
                    Nota = await _recetas.Amasado(consolidado.Productos)
                },
                new Titular
                {
                    Titulo = "Empanadas",
                    Valor = consolidado.Empanadas.ToString(),
                    Nota = $"{consolidado.Gustos} {(consolidado.Gustos == 1 ? "gusto" : "gustos")}"
                },
                new Titular
                {
                    Titulo = "Pedidos a cubrir",
                    Valor = marco.SinEntregar.ToString(),
                    // el pie no repite el numero: dice de cuando es el mas viejo,
                    // que es el dato que avisa si algo se quedo atras
                    Nota = masVieja is DateTime cuando
                        ? $"el más viejo, de las {Reloj.EnBuenosAires(cuando):HH:mm}"
                        : "no hay pedidos abiertos"
                }
            ],
            Grupos =
            [
                .. familias
                    .Select(f => new GrupoProduccion
                    {
                        Familia = f.Nombre,
                        Total = consolidado.Productos.Where(x => x.Familia == f.Familia).Sum(x => x.Piezas),
                        Renglones =
                        [
                            .. consolidado.Productos
                                .Where(x => x.Familia == f.Familia)
                                .Select(x => new RenglonProduccion
                                {
                                    Cuantas = x.Piezas,
                                    Nombre = x.Nombre,
                                    EnPastilla = f.Familia != Familia.Empanada,
                                    Detalle = f.Familia == Familia.Empanada
                                        ? [.. x.Packs
                                            .OrderByDescending(p => p.Key)
                                            .Select(p => $"{p.Value} {(p.Value == 1 ? "pack" : "packs")} de {p.Key}")]
                                        // las combinaciones ya vienen ordenadas y suman las piezas
                                        : [.. x.Combinaciones.Select(c => $"{c.Piezas} {c.Como}")]
                                })
                        ]
                    })
                    .Where(x => x.Renglones.Count > 0)
            ]
        });
    }

    // La lista de pedidos y el que se este mirando.
    //
    // El filtro y el elegido viajan por la direccion y no por sesion: asi cada
    // pantalla se puede compartir, marcar y recargar, y el boton de atras
    // vuelve a lo que se estaba mirando.
    [HttpGet("pedidos")]
    public async Task<IActionResult> Pedidos(string? filtro = null, int? pedido = null)
    {
        filtro = Filtros.Todo.Any(x => x.Clave == filtro) ? filtro! : Filtros.Activos;

        var todos = _contexto.Pedidos.AsQueryable();
        var vistos = filtro switch
        {
            Filtros.Activos => todos.Where(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando),
            Filtros.Nuevos => todos.Where(x => x.Estado == EstadoPedido.Nuevo),
            Filtros.Preparando => todos.Where(x => x.Estado == EstadoPedido.Preparando),
            Filtros.Entregados => todos.Where(x => x.Estado == EstadoPedido.Entregado),
            _ => todos
        };

        var lista = await vistos
            // el ultimo arriba: lo que entro recien es lo que se esta mirando
            .OrderByDescending(x => x.FechaPedido)
            .Select(x => new FilaPedido
            {
                IdPedido = x.IdPedido,
                Cliente = x.Cliente,
                Estado = x.Estado,
                FechaPedido = x.FechaPedido,
                Total = x.Items.Sum(i => i.Cantidad * i.PrecioUnitario)
            })
            .ToListAsync();

        // El pedido pedido por direccion puede no estar en la lista que se ve
        // -se filtro por entregados y viene el numero de uno nuevo-. En ese caso
        // se muestra el primero, que es lo que la persona esta mirando igual.
        var elegido = lista.FirstOrDefault(x => x.IdPedido == pedido) ?? lista.FirstOrDefault();

        var marco = await Marco();

        return View(new PedidosVm
        {
            Abierta = marco.Abierta,
            SinEntregar = marco.SinEntregar,
            Filtro = filtro,
            Lista = lista,
            Elegido = elegido is null ? null : await Detalle(elegido.IdPedido),
            SinAbrir = await _contexto.Pedidos.CountAsync(x => x.Estado == EstadoPedido.Nuevo),
            EnTotal = await _contexto.Pedidos.CountAsync()
        });
    }

    // Avanzar un pedido: nuevo va a preparando y preparando a entregado. El
    // camino lo decide el modelo, no esta pantalla: aca no se puede saltear un
    // paso ni volver atras porque no hay a donde mandarlo.
    [HttpPost("pedidos/avanzar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id, string? filtro = null)
    {
        var pedido = await _contexto.Pedidos.FindAsync(id)
            ?? throw new InvalidOperationException($"No existe el pedido {id}.");

        // Si ya lo avanzo desde otra pestana, no pasa nada: el estado que hay es
        // el que vale. Volver a apretar no lo manda a entregado de una.
        if (pedido.Siguiente is EstadoPedido siguiente)
        {
            pedido.Estado = siguiente;
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Pedidos), new { filtro, pedido = id });
    }

    // Cancelar. Sale de todos los calculos pero no del historial: por eso se
    // marca el estado y no se borra la fila.
    [HttpPost("pedidos/cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, string? filtro = null)
    {
        var pedido = await _contexto.Pedidos.FindAsync(id)
            ?? throw new InvalidOperationException($"No existe el pedido {id}.");

        // uno entregado ya no se cancela: la pizza salio y se cobro
        if (pedido.SinEntregar)
        {
            pedido.Estado = EstadoPedido.Cancelado;
            await _contexto.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Pedidos), new { filtro, pedido = id });
    }

    private async Task<DetallePedido?> Detalle(int id)
    {
        var pedido = await _contexto.Pedidos
            .Where(x => x.IdPedido == id)
            .Select(x => new
            {
                x.IdPedido,
                x.Cliente,
                x.Telefono,
                x.Direccion,
                x.FechaPedido,
                x.Estado,
                Items = x.Items
                    .Select(i => new
                    {
                        i.Cantidad,
                        i.Producto.Nombre,
                        i.Producto.Familia,
                        i.IdProducto,
                        i.UnidadesPorPack,
                        Total = i.Cantidad * i.PrecioUnitario,
                        // los nombres copiados al confirmar, no los de hoy
                        Sacados = i.Quitados.Select(q => q.Ingrediente).OrderBy(q => q).ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (pedido is null)
        {
            return null;
        }

        return new DetallePedido
        {
            IdPedido = pedido.IdPedido,
            Cliente = pedido.Cliente,
            Telefono = pedido.Telefono,
            Direccion = pedido.Direccion,
            FechaPedido = pedido.FechaPedido,
            Estado = pedido.Estado,
            Total = pedido.Items.Sum(x => x.Total),
            Items =
            [
                // El orden se pone aca y no en la consulta. Familia se guarda
                // como texto, asi que un ORDER BY en la base sale alfabetico
                // -Empanada, Focaccia, Pizza- y la carta va al reves. En memoria
                // ordena por el valor del enum, que es el orden de la carta y el
                // mismo que usa el aviso de Telegram.
                .. pedido.Items
                    .OrderBy(x => x.Familia)
                    .ThenBy(x => x.IdProducto)
                    .Select(x => new ItemDelDetalle
                {
                    Cantidad = x.Cantidad,
                    // el pack se nombra por lo que es: una caja de doce de un
                    // solo gusto, y no doce empanadas sueltas
                    Nombre = x.UnidadesPorPack is int u
                        ? $"Pack de {u} · {x.Nombre.ToLowerInvariant()}"
                        : x.Nombre,
                    Sin = x.Sacados.Count == 0 ? "" : "sin " + string.Join(", ", x.Sacados),
                    Total = x.Total
                })
            ]
        };
    }

    // Que hay que hornear, agrupado por familia. El desglose lo arma el
    // servicio; aca solo se junta en las tres familias y se le pone nombre.
    //
    // Las combinaciones se juntan a proposito. Tres margaritas son tres
    // margaritas aunque una vaya sin albahaca: al horno entran las tres igual, y
    // lo que se le saca a cada una se lee en Produccion, no aca.
    private static IReadOnlyList<GrupoHornear> Hornear(Consolidado consolidado)
    {
        (string Nombre, Familia Familia, bool PorUnidad)[] familias =
        [
            ("Pizzas", Familia.Pizza, false),
            ("Focaccias", Familia.Focaccia, false),
            ("Empanadas", Familia.Empanada, true)
        ];

        return
        [
            .. familias
                .Select(f => new GrupoHornear
                {
                    Familia = f.Nombre,
                    PorUnidad = f.PorUnidad,
                    Renglones =
                    [
                        .. consolidado.Productos
                            .Where(x => x.Familia == f.Familia)
                            .Select(x => new RenglonHornear { Nombre = x.Nombre, Cuantas = x.Piezas })
                    ]
                })
                // una familia sin nada no se muestra: un titulo con nada abajo
                // se lee como que falta algo
                .Where(x => x.Renglones.Count > 0)
        ];
    }

    // Las cinco tarjetas del tablero.
    //
    // La maqueta trae un catalogo de veintiseis cifras y deja elegir cinco desde
    // el titulo de cada tarjeta. Estas cinco salen de ese catalogo y son las que
    // se pueden calcular hoy: las otras tres que trae de fabrica -Falta comprar,
    // Produccion y Costo- necesitan las recetas y el stock, que son las pantallas
    // que faltan. Cuando existan, entra el selector y vuelven las de la maqueta.
    private async Task<IReadOnlyList<Cifra>> Cifras()
    {
        // un solo viaje: todo lo que sigue sale de los pedidos sin entregar, y
        // son pocos por definicion -los entregados no cuentan-
        var abiertos = await _contexto.Pedidos
            .Where(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando)
            .Select(x => new
            {
                x.IdPedido,
                x.Telefono,
                x.FechaPedido,
                Nuevo = x.Estado == EstadoPedido.Nuevo,
                Plata = x.Items.Sum(i => i.Cantidad * i.PrecioUnitario)
            })
            .ToListAsync();

        var viejo = abiertos.OrderBy(x => x.FechaPedido).FirstOrDefault();

        return
        [
            new Cifra
            {
                Titulo = "Pedidos nuevos",
                Valor = abiertos.Count(x => x.Nuevo).ToString(),
                Nota = "sin abrir"
            },
            new Cifra
            {
                Titulo = "Sin entregar",
                Valor = abiertos.Count.ToString(),
                Nota = "abiertos en total"
            },
            new Cifra
            {
                Titulo = "El más viejo",
                Valor = viejo is null ? "—" : $"{Reloj.HorasDesde(viejo.FechaPedido)} h",
                Nota = viejo is null
                    ? "no hay pedidos abiertos"
                    : $"el {viejo.IdPedido:0000}, de las {Reloj.EnBuenosAires(viejo.FechaPedido):HH:mm}"
            },
            new Cifra
            {
                Titulo = "Clientes",
                // por telefono y no por nombre: dos Juan son dos personas, y el
                // mismo telefono pidiendo dos veces es una sola esperando
                Valor = abiertos.Select(x => x.Telefono).Distinct().Count().ToString(),
                Nota = "esperando"
            },
            new Cifra
            {
                Titulo = "Total",
                Valor = abiertos.Sum(x => x.Plata).ToString("C"),
                Nota = "sin entregar todavía"
            }
        ];
    }

    // El interruptor. Por POST porque cambia algo, y volviendo a Inicio para que
    // recargar la pagina no lo vuelva a tocar.
    [HttpPost("llave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Llave()
    {
        var tienda = await _contexto.Tienda.SingleOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "Falta la fila de la tienda. Corre dotnet ef database update.");

        tienda.Abierta = !tienda.Abierta;
        await _contexto.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // Lo que necesita el marco, que se dibuja en todas las pantallas del panel
    private async Task<PanelVm> Marco() => new()
    {
        Abierta = await _contexto.Tienda.AnyAsync(x => x.Abierta),
        SinEntregar = await _contexto.Pedidos
            .CountAsync(x => x.Estado == EstadoPedido.Nuevo || x.Estado == EstadoPedido.Preparando)
    };

    [AllowAnonymous]
    [HttpGet("entrar")]
    public IActionResult Entrar(string? volverA = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        // Sin clave configurada el panel queda cerrado, no abierto. Es la única
        // manera de equivocarse que no se puede permitir acá.
        ViewData["SinClave"] = string.IsNullOrWhiteSpace(_clave);
        ViewData["VolverA"] = Seguro(volverA);
        return View();
    }

    [AllowAnonymous]
    [HttpPost("entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(string? clave, string? volverA)
    {
        if (string.IsNullOrWhiteSpace(_clave))
        {
            ViewData["SinClave"] = true;
            return View();
        }

        if (!Coincide(clave ?? ""))
        {
            await Task.Delay(Castigo);
            // no dice si la clave era corta, larga o parecida: solo que no es
            ViewData["Error"] = "Esa no es la clave.";
            ViewData["VolverA"] = Seguro(volverA);
            return View();
        }

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "vendedor")],
            CookieAuthenticationDefaults.AuthenticationScheme);

        // persistente: es su computadora y su teléfono, y tener que escribir la
        // clave cada vez que abre el panel un sábado a la mañana no protege nada
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect(Seguro(volverA) ?? Url.Action(nameof(Index))!);
    }

    // por POST y no por un enlace: un GET que cierra la sesión lo dispara
    // cualquier página ajena con una etiqueta de imagen apuntada acá
    [HttpPost("salir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    // La comparación es de tiempo fijo: una común corta apenas encuentra la
    // primera letra distinta, y esa diferencia de microsegundos, medida muchas
    // veces, deja adivinar la clave letra por letra.
    private bool Coincide(string tecleada) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(tecleada),
            Encoding.UTF8.GetBytes(_clave!));

    // La dirección a la que volver viene de la cookie, que la arma el middleware,
    // pero llega por la query y cualquiera la puede escribir. Si no es de este
    // sitio no se usa: si no, esto es un trampolín para mandar a otro lado.
    private string? Seguro(string? destino) =>
        !string.IsNullOrWhiteSpace(destino) && Url.IsLocalUrl(destino) ? destino : null;
}
