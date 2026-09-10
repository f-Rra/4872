using f4872.Data;
using f4872.Models;
using f4872.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace f4872.Services;

// Convierte lo que manda el checkout en un pedido guardado.
//
// La regla de toda la clase: del navegador se cree QUÉ se pidió y nada más. El
// precio, el nombre y si sigue estando en la carta se vuelven a leer de la base
// acá adentro. Un pedido con el precio que dijo el cliente es un pedido que
// cualquiera puede escribir a mano.
public class PedidoService
{
    private readonly Contexto _contexto;
    private readonly TelegramService _telegram;

    // Nadie pide noventa y nueve pizzas iguales. El tope no está para el que
    // compra: está para que un renglón manoteado no deje un pedido absurdo
    // esperando en el panel.
    private const int TopePorRenglon = 99;

    public PedidoService(Contexto contexto, TelegramService telegram)
    {
        _contexto = contexto;
        _telegram = telegram;
    }

    // un renglón del pedido ya leído: qué producto, cuántos, de qué pack si es
    // empanada, y qué ingredientes se le sacaron
    private sealed record Renglon(int IdProducto, int Cantidad, int? Unidades, IReadOnlyList<int> Sin);

    // lo único que hace falta saber de un producto para cobrarlo
    private sealed record Carta(int IdProducto, string Nombre, decimal? Precio, bool Activo, Familia Familia);

    public async Task<int> Confirmar(PedidoNuevo datos)
    {
        var cliente = (datos.Cliente ?? "").Trim();
        var direccion = (datos.Direccion ?? "").Trim();
        var telefono = (datos.Telefono ?? "").Trim();

        // Las mismas tres reglas que apagan el botón en la pantalla, repetidas
        // acá. No es duplicado al pedo: la pantalla se puede saltear y este es
        // el único lado que decide de verdad.
        if (cliente.Length < 2)
        {
            throw new InvalidOperationException("Falta tu nombre.");
        }

        if (direccion.Length < 5)
        {
            throw new InvalidOperationException("Falta la dirección donde entregamos.");
        }

        if (telefono.Count(char.IsDigit) < 8)
        {
            throw new InvalidOperationException("El WhatsApp tiene que tener al menos ocho dígitos.");
        }

        if (datos.Items is null || datos.Items.Count == 0)
        {
            throw new InvalidOperationException("El pedido está vacío.");
        }

        // Lo primero que se mira contra la base. Una pantalla abierta desde
        // antes sigue pudiendo postear aunque la carta ya diga que está
        // cerrada, y un pedido que entra con la tienda cerrada es uno que él no
        // va a ver hasta que la abra.
        if (!await _contexto.Tienda.AnyAsync(x => x.Abierta))
        {
            throw new InvalidOperationException(
                "Esta semana no tomamos pedidos. Volvemos a tomar pedidos el martes.");
        }

        var renglones = datos.Items.Select(x => Leer(x.Key, x.Value)).ToList();
        var ids = renglones.Select(x => x.IdProducto).Distinct().ToList();

        var productos = await _contexto.Productos
            .Where(x => ids.Contains(x.IdProducto))
            .Select(x => new Carta(x.IdProducto, x.Nombre, x.Precio, x.Activo, x.Familia))
            .ToDictionaryAsync(x => x.IdProducto);

        var unidades = renglones
            .Where(x => x.Unidades is not null)
            .Select(x => x.Unidades!.Value)
            .Distinct()
            .ToList();

        var packs = await _contexto.Packs
            .Where(x => x.Activo && unidades.Contains(x.Unidades))
            .ToDictionaryAsync(x => x.Unidades, x => x.Precio);

        // los quitables por par producto–ingrediente, que es donde vive el
        // permiso: la muzzarella se saca de una fugazzeta y de una napolitana no
        var quitables = await _contexto.ProductoIngredientes
            .Where(x => ids.Contains(x.IdProducto) && x.Quitable)
            .Select(x => new { x.IdProducto, x.IdIngrediente, x.Ingrediente.Nombre })
            .ToDictionaryAsync(x => (x.IdProducto, x.IdIngrediente), x => x.Nombre);

        var pedido = new Pedido
        {
            Cliente = cliente,
            Telefono = telefono,
            Direccion = direccion,
            Estado = EstadoPedido.Nuevo
        };

        foreach (var renglon in renglones)
        {
            pedido.Items.Add(Armar(renglon, productos, packs, quitables));
        }

        // la fecha no se toca: la pone Postgres con su propio reloj, que es el
        // mismo para un pedido de la web y para uno cargado a mano
        _contexto.Pedidos.Add(pedido);
        await _contexto.SaveChangesAsync();

        // Recién acá, con el pedido ya guardado. Se espera a que salga en vez
        // de largarlo por atrás: son unos 600 ms y a cambio queda registrado en
        // el log si falló. Y aunque falle, no se cae nada: eso lo resuelve el
        // servicio adentro.
        await _telegram.Avisar(Aviso(pedido, productos));

        return pedido.IdPedido;
    }

    // El mensaje que le llega al teléfono. Lleva lo que hace falta para saber
    // si hay que ponerse a amasar y para poder escribirle sin abrir el panel:
    // qué se pidió, cuánto es, y a quién y dónde.
    private static string Aviso(Pedido pedido, IReadOnlyDictionary<int, Carta> productos)
    {
        var renglones = pedido.Items
            // el mismo orden que la carta, para poder compararlos de un vistazo
            .OrderBy(x => productos[x.IdProducto].Familia)
            .ThenBy(x => x.IdProducto)
            .Select(x =>
            {
                var nombre = TelegramService.Escapar(productos[x.IdProducto].Nombre);
                var pack = x.UnidadesPorPack is int u ? $" · x{u}" : "";
                var sin = x.Sin.Length == 0 ? "" : $" — {TelegramService.Escapar(x.Sin)}";
                return $"{x.Cantidad}× {nombre}{pack}{sin}";
            });

        return $"<b>Pedido {pedido.IdPedido:0000}</b>\n\n" +
               string.Join("\n", renglones) +
               $"\n\n<b>Total {pedido.Total:C}</b>\n\n" +
               $"{TelegramService.Escapar(pedido.Cliente)}\n" +
               $"{TelegramService.Escapar(pedido.Direccion)}\n" +
               TelegramService.Escapar(pedido.Telefono);
    }

    // Las claves las escribe tienda.js y son tres formas:
    //   p12       una pizza o una focaccia
    //   p12|3,7   la misma, sin los ingredientes 3 y 7
    //   e5x12     un pack de 12 empanadas del gusto 5
    // Una clave que no sea ninguna de las tres no es un pedido viejo: el
    // navegador poda los viejos antes de mandar. Es alguien escribiendo a mano.
    private static Renglon Leer(string clave, int cantidad)
    {
        if (cantidad < 1 || cantidad > TopePorRenglon)
        {
            throw new InvalidOperationException($"El pedido trae una cantidad imposible: {cantidad}.");
        }

        if (clave.StartsWith('e'))
        {
            var partes = clave[1..].Split('x');
            if (partes.Length != 2 ||
                !int.TryParse(partes[0], out var gusto) || gusto < 1 ||
                !int.TryParse(partes[1], out var unidades) || unidades < 1)
            {
                throw new InvalidOperationException("El pedido trae un pack que no se entiende.");
            }

            return new Renglon(gusto, cantidad, unidades, []);
        }

        var trozos = clave.StartsWith('p') ? clave[1..].Split('|') : [];
        if (trozos.Length is 0 or > 2 || !int.TryParse(trozos[0], out var idProducto) || idProducto < 1)
        {
            throw new InvalidOperationException("El pedido trae un renglón que no se entiende.");
        }

        var sin = new List<int>();
        if (trozos.Length == 2 && trozos[1].Length > 0)
        {
            foreach (var texto in trozos[1].Split(','))
            {
                if (!int.TryParse(texto, out var idIngrediente) || idIngrediente < 1)
                {
                    throw new InvalidOperationException("El pedido trae un ingrediente que no se entiende.");
                }

                // repetido es lo mismo que una vez: sacar dos veces la albahaca
                // no es sacarla más, y la tabla tiene el par como clave
                if (!sin.Contains(idIngrediente))
                {
                    sin.Add(idIngrediente);
                }
            }
        }

        return new Renglon(idProducto, cantidad, null, sin);
    }

    private static ItemPedido Armar(
        Renglon renglon,
        IReadOnlyDictionary<int, Carta> productos,
        IReadOnlyDictionary<int, decimal> packs,
        IReadOnlyDictionary<(int, int), string> quitables)
    {
        if (!productos.TryGetValue(renglon.IdProducto, out var producto))
        {
            throw new InvalidOperationException(
                "Hay algo en el pedido que ya no está en la carta. Volvé a la carta y armalo de nuevo.");
        }

        // El que se acabó se nombra. Es el único error de todos estos que le
        // puede pasar a alguien que no hizo nada raro, y necesita saber cuál
        // sacar para poder seguir.
        if (!producto.Activo)
        {
            throw new InvalidOperationException(
                $"Se acabó: {producto.Nombre}. Sacalo del pedido y confirmá de nuevo.");
        }

        if (renglon.Unidades is int cuantas)
        {
            if (producto.Familia != Familia.Empanada)
            {
                throw new InvalidOperationException($"{producto.Nombre} no se vende por pack.");
            }

            if (!packs.TryGetValue(cuantas, out var precioPack))
            {
                throw new InvalidOperationException($"El pack de {cuantas} ya no está.");
            }

            return new ItemPedido
            {
                IdProducto = producto.IdProducto,
                Cantidad = renglon.Cantidad,
                UnidadesPorPack = cuantas,
                PrecioUnitario = precioPack
            };
        }

        if (producto.Precio is not decimal precio)
        {
            throw new InvalidOperationException($"{producto.Nombre} se vende por pack, no por unidad.");
        }

        var item = new ItemPedido
        {
            IdProducto = producto.IdProducto,
            Cantidad = renglon.Cantidad,
            PrecioUnitario = precio
        };

        foreach (var idIngrediente in renglon.Sin)
        {
            // Si dejó de ser quitable no se guarda el «sin» y listo: el cliente
            // pidió algo sin un ingrediente que hoy no se puede sacar. Cortar
            // acá es lo seguro, porque lo otro es mandarle a la cocina una pizza
            // con algo que la persona dijo que no.
            if (!quitables.TryGetValue((producto.IdProducto, idIngrediente), out var nombre))
            {
                throw new InvalidOperationException(
                    $"Cambió la receta de {producto.Nombre} mientras armabas el pedido. Volvé a la carta y fijate.");
            }

            item.Quitados.Add(new ItemQuitado { Ingrediente = nombre });
        }

        return item;
    }
}
