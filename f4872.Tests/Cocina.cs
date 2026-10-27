using f4872.Data;
using f4872.Models;

namespace f4872.Tests;

// Una cocina chica con números redondos, para que cada resultado de los tests de cálculos se pueda
// verificar a mano. Todos los precios son por gramo (o por unidad) y salen de «lo que trae la compra»
// y «cuánto sale»:
//
//   Harina       $1.500 el kilo   →  $1,5 el gramo
//   Muzzarella   $8.000 el kilo   →  $8 el gramo
//   Tomate       $2.000 el kilo   →  $2 el gramo
//   Carne        $10.000 el kilo  →  $10 el gramo
//   Sal          $1.000 el kilo   →  $1 el gramo
//   Albahaca     $50 el atado de 10  →  $5 la unidad
//   Agua         no se compra (libre): cuesta cero de verdad
//
// Las recetas se cargan enteras, con su rinde, como se cocinan:
//
//   Bollo de pizza     rinde 6    1 kg de harina + 600 ml de agua
//   Bollo de focaccia  rinde 4    1 kg de harina + 750 ml de agua
//   Salsa de tomate    rinde 10   1 kg de tomate
//   Relleno de carne   rinde 12   600 g de carne
public static class Cocina
{
    // ingredientes
    public const int Harina = 10;
    public const int Agua = 11;
    public const int Muzzarella = 12;
    public const int Albahaca = 13;
    public const int Tomate = 14;
    public const int Carne = 15;
    public const int Sal = 16;

    // recetas
    public const int BasePizza = 1;
    public const int BaseFocaccia = 2;
    public const int SalsaDeTomate = 3;
    public const int RellenoDeCarne = 4;

    // productos
    public const int Margarita = 1;   // pizza: muzzarella 120 g y 4 de albahaca, con salsa de tomate
    public const int Fugazzeta = 2;   // pizza: muzzarella 200 g
    public const int Romero = 4;      // focaccia: 5 g de sal
    public const int CarneSuave = 5;  // empanada: todo viene en el relleno

    public const decimal PrecioMargarita = 9800m;
    public const decimal PrecioFugazzeta = 11200m;
    public const decimal PrecioRomero = 6800m;

    public static Contexto Armar()
    {
        var contexto = DatosDePrueba.CrearContexto();

        contexto.Ingredientes.AddRange(
            Comprado(Harina, "Harina 00", Medida.Gramo, stock: 5000, bulto: 1000, precio: 1500),
            new Ingrediente { IdIngrediente = Agua, Nombre = "Agua", Unidad = Medida.Mililitro, Libre = true },
            Comprado(Muzzarella, "Muzzarella", Medida.Gramo, stock: 200, bulto: 1000, precio: 8000),
            Comprado(Albahaca, "Albahaca", Medida.Unidad, stock: 3, bulto: 10, precio: 50),
            Comprado(Tomate, "Tomate", Medida.Gramo, stock: 0, bulto: 1000, precio: 2000),
            Comprado(Carne, "Carne", Medida.Gramo, stock: 0, bulto: 1000, precio: 10000),
            Comprado(Sal, "Sal fina", Medida.Gramo, stock: 0, bulto: 1000, precio: 1000));

        contexto.Recetas.AddRange(
            new Receta { IdReceta = BasePizza, Nombre = "Bollo de pizza", Tipo = TipoReceta.Base, Familia = Familia.Pizza, Rinde = 6 },
            new Receta { IdReceta = BaseFocaccia, Nombre = "Bollo de focaccia", Tipo = TipoReceta.Base, Familia = Familia.Focaccia, Rinde = 4 },
            new Receta { IdReceta = SalsaDeTomate, Nombre = "Salsa de tomate", Tipo = TipoReceta.Salsa, Rinde = 10 },
            new Receta { IdReceta = RellenoDeCarne, Nombre = "Relleno de carne", Tipo = TipoReceta.Relleno, Rinde = 12 });

        contexto.RecetaIngredientes.AddRange(
            new RecetaIngrediente { IdReceta = BasePizza, IdIngrediente = Harina, Cantidad = 1000 },
            new RecetaIngrediente { IdReceta = BasePizza, IdIngrediente = Agua, Cantidad = 600 },
            new RecetaIngrediente { IdReceta = BaseFocaccia, IdIngrediente = Harina, Cantidad = 1000 },
            new RecetaIngrediente { IdReceta = BaseFocaccia, IdIngrediente = Agua, Cantidad = 750 },
            new RecetaIngrediente { IdReceta = SalsaDeTomate, IdIngrediente = Tomate, Cantidad = 1000 },
            new RecetaIngrediente { IdReceta = RellenoDeCarne, IdIngrediente = Carne, Cantidad = 600 });

        contexto.Productos.AddRange(
            new Producto { IdProducto = Margarita, Familia = Familia.Pizza, Nombre = "Margarita", Precio = PrecioMargarita, Posicion = 1, IdBase = BasePizza },
            new Producto { IdProducto = Fugazzeta, Familia = Familia.Pizza, Nombre = "Fugazzeta", Precio = PrecioFugazzeta, Posicion = 2, IdBase = BasePizza },
            new Producto { IdProducto = Romero, Familia = Familia.Focaccia, Nombre = "Romero y sal", Precio = PrecioRomero, Posicion = 1, IdBase = BaseFocaccia },
            new Producto { IdProducto = CarneSuave, Familia = Familia.Empanada, Nombre = "Carne suave", Precio = null, Posicion = 1, IdRelleno = RellenoDeCarne });

        contexto.ProductoIngredientes.AddRange(
            new ProductoIngrediente { IdProducto = Margarita, IdIngrediente = Muzzarella, Cantidad = 120, Posicion = 1 },
            new ProductoIngrediente { IdProducto = Margarita, IdIngrediente = Albahaca, Cantidad = 4, Posicion = 2, Quitable = true },
            new ProductoIngrediente { IdProducto = Fugazzeta, IdIngrediente = Muzzarella, Cantidad = 200, Posicion = 1 },
            new ProductoIngrediente { IdProducto = Romero, IdIngrediente = Sal, Cantidad = 5, Posicion = 1 });

        contexto.ProductoSalsas.Add(
            new ProductoSalsa { IdProducto = Margarita, IdReceta = SalsaDeTomate, Porciones = 1, Quitable = true });

        // El pack de 12 es el más barato por unidad: $1.100 contra $1.200 del de 6. El de 24 está
        // apagado y saldría más barato todavía ($833 la unidad): si se contara, se vería.
        contexto.Packs.AddRange(
            new Pack { Unidades = 6, Precio = 7200m },
            new Pack { Unidades = 12, Precio = 13200m },
            new Pack { Unidades = 24, Precio = 20000m, Activo = false });

        contexto.SaveChanges();
        return contexto;
    }

    private static Ingrediente Comprado(int id, string nombre, Medida unidad, decimal stock, decimal bulto, decimal precio) =>
        new()
        {
            IdIngrediente = id,
            Nombre = nombre,
            Unidad = unidad,
            Stock = stock,
            CantidadDeCompra = bulto,
            PrecioDeCompra = precio
        };

    // ---------- pedidos ----------

    // Un renglón de pedido. El precio es el de cuando se confirmó, y se guarda en el renglón.
    public static ItemPedido Item(int idProducto, int cantidad, decimal precio, int? pack = null, params string[] sin) =>
        new()
        {
            IdProducto = idProducto,
            Cantidad = cantidad,
            PrecioUnitario = precio,
            UnidadesPorPack = pack,
            Quitados = sin.Select(nombre => new ItemQuitado { Ingrediente = nombre }).ToList()
        };

    public static Pedido Pedir(Contexto contexto, EstadoPedido estado, params ItemPedido[] items) =>
        PedirEl(contexto, estado, DateTime.UtcNow, items);

    // La semana de referencia, con un pedido en cada estado:
    //
    //   Nuevo        3 margaritas, 2 margaritas sin albahaca y 1 romero
    //   Preparando   1 fugazzeta y 1 pack de 12 de carne suave
    //   Entregado    10 margaritas   \  no cuentan: ya salieron o no se hacen
    //   Cancelado    10 margaritas   /
    //
    // O sea que hay que hacer 5 margaritas, 1 fugazzeta, 1 romero y 12 empanadas.
    public static void LaSemana(Contexto contexto)
    {
        Pedir(contexto, EstadoPedido.Nuevo,
            Item(Margarita, 3, PrecioMargarita),
            Item(Margarita, 2, PrecioMargarita, null, "Albahaca"),
            Item(Romero, 1, PrecioRomero));

        Pedir(contexto, EstadoPedido.Preparando,
            Item(Fugazzeta, 1, PrecioFugazzeta),
            Item(CarneSuave, 1, 13200m, pack: 12));

        Pedir(contexto, EstadoPedido.Entregado, Item(Margarita, 10, PrecioMargarita));
        Pedir(contexto, EstadoPedido.Cancelado, Item(Margarita, 10, PrecioMargarita));
    }

    // Las cuentas con plata dividen entre rindes (1000 / 6 · 1,5), y un decimal no guarda
    // un tercio exacto: la diferencia está más allá del décimo decimal, que es donde no importa.
    public static void Igual(decimal esperado, decimal? real)
    {
        Assert.NotNull(real);
        Assert.Equal(esperado, real!.Value, 6);
    }

    public static Pedido PedirEl(Contexto contexto, EstadoPedido estado, DateTime fechaUtc, params ItemPedido[] items)
    {
        var pedido = new Pedido
        {
            Cliente = "Cliente de prueba",
            Telefono = "11 5555-0101",
            Direccion = "Calle falsa 123",
            Estado = estado,
            FechaPedido = fechaUtc,
            Items = items.ToList()
        };

        contexto.Pedidos.Add(pedido);
        contexto.SaveChanges();
        return pedido;
    }
}
