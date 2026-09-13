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

        producto.Nombre = nombre;
        producto.Familia = ficha.Familia;
        producto.Precio = precio;

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
        ficha.Packs = vm.Ficha.Packs;
        vm.Ficha = ficha;
        vm.EsNuevo = nuevo;
        vm.Error = error;
        return View(nameof(Productos), vm);
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
                ? new FichaProducto { Packs = packs }
                : new FichaProducto
                {
                    IdProducto = elegido.IdProducto,
                    Nombre = elegido.Nombre,
                    Familia = elegido.Familia,
                    Precio = elegido.Precio,
                    Activo = elegido.Activo,
                    Packs = packs,
                    Receta = receta,
                    Disponibles = [.. disponibles.Select(x => new IngredienteDisponible
                    {
                        Nombre = x.Nombre,
                        Unidad = Cantidades.Abreviatura(x.Unidad)
                    })],
                    Sumando = enEspera
                }
        };
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
