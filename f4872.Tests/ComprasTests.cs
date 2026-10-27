using f4872.Models;
using f4872.Services;
using static f4872.Tests.Cocina;

namespace f4872.Tests;

// Cuánto de cada ingrediente se come lo pedido (Necesita), de dónde sale cada parte de ese número
// (Desglose) y qué hay que ir a comprar (FaltaComprar). Los tres salen de una sola cuenta, y los
// tests miran justamente eso: que no puedan decir cosas distintas.
//
// Con LaSemana (5 margaritas, 1 fugazzeta, 1 romero y 12 empanadas) el pedido se come:
//
//   Harina       2000 g   una receta de bollo de pizza + una de focaccia
//   Agua         1350 ml  600 + 750
//   Muzzarella    800 g   5 margaritas × 120 + 1 fugazzeta × 200
//   Albahaca       12 u   solo las 3 margaritas que la llevan × 4
//   Tomate       1000 g   una receta de salsa (5 margaritas, rinde 10)
//   Carne         600 g   una receta de relleno (12 empanadas, rinde 12)
//   Sal             5 g   1 romero × 5
public class ComprasTests
{
    // ---------- Necesita ----------

    [Fact]
    public async Task Necesita_sin_pedidos_no_necesita_nada()
    {
        using var contexto = Armar();

        Assert.Empty(await new RecetaService(contexto).Necesita());
    }

    [Fact]
    public async Task Necesita_suma_lo_que_come_cada_parte_del_pedido()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var necesita = await new RecetaService(contexto).Necesita();

        Assert.Equal(new Dictionary<int, decimal>
        {
            [Harina] = 2000,
            [Agua] = 1350,
            [Muzzarella] = 800,
            [Albahaca] = 12,
            [Tomate] = 1000,
            [Carne] = 600,
            [Sal] = 5
        }, necesita);
    }

    [Fact]
    public async Task Necesita_el_ingrediente_sacado_no_se_cuenta_pero_la_masa_si()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo, Item(Margarita, 5, PrecioMargarita, null, "Albahaca"));

        var necesita = await new RecetaService(contexto).Necesita();

        // nadie se come la albahaca: no hay que comprarla. Pero el bollo se hace entero igual
        Assert.False(necesita.ContainsKey(Albahaca));
        Assert.Equal(1000, necesita[Harina]);
        Assert.Equal(600, necesita[Muzzarella]);
    }

    [Fact]
    public async Task Necesita_las_recetas_se_hacen_enteras_nunca_a_medias()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo,
            Item(Margarita, 4, PrecioMargarita),
            Item(Fugazzeta, 3, PrecioFugazzeta));

        var necesita = await new RecetaService(contexto).Necesita();

        // 7 bollos de a 6 son dos recetas: 2 kg de harina, no 1,17. Se compra para lo que se amasa
        Assert.Equal(2000, necesita[Harina]);
        Assert.Equal(1200, necesita[Agua]);
    }

    [Fact]
    public async Task Necesita_la_masa_de_pizza_y_la_de_focaccia_no_se_mezclan()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo,
            Item(Margarita, 1, PrecioMargarita),
            Item(Romero, 1, PrecioRomero));

        var necesita = await new RecetaService(contexto).Necesita();

        // si compartieran bollo, 2 piezas serían una sola receta y alcanzaría 1 kg
        Assert.Equal(2000, necesita[Harina]);
        Assert.Equal(1350, necesita[Agua]);
    }

    [Theory]
    [InlineData(3, 12, 1000)]   // con salsa solo 3 pizzas: una receta
    [InlineData(10, 0, 1000)]   // justo las 10 que rinde
    [InlineData(11, 0, 2000)]   // una más y hay que hacer otra
    [InlineData(0, 15, 0)]      // todas la piden sin salsa: no se hace
    public async Task Necesita_la_salsa_se_hace_para_las_pizzas_que_la_llevan(int conSalsa, int sinSalsa, decimal tomate)
    {
        using var contexto = Armar();
        var renglones = new List<ItemPedido>();

        if (conSalsa > 0)
        {
            renglones.Add(Item(Margarita, conSalsa, PrecioMargarita));
        }

        if (sinSalsa > 0)
        {
            renglones.Add(Item(Margarita, sinSalsa, PrecioMargarita, null, "Salsa de tomate"));
        }

        Pedir(contexto, EstadoPedido.Nuevo, [.. renglones]);

        var necesita = await new RecetaService(contexto).Necesita();

        Assert.Equal(tomate, necesita.GetValueOrDefault(Tomate));
    }

    [Fact]
    public async Task Necesita_media_porcion_de_salsa_cuenta_por_media()
    {
        using var contexto = Armar();
        contexto.ProductoSalsas.Single().Porciones = 0.5m;
        contexto.SaveChanges();
        Pedir(contexto, EstadoPedido.Nuevo, Item(Margarita, 21, PrecioMargarita));

        var necesita = await new RecetaService(contexto).Necesita();

        // 21 pizzas a media porción son 10,5: dos recetas de 10. A porción entera serían 21, tres recetas
        Assert.Equal(2000, necesita[Tomate]);
    }

    [Fact]
    public async Task Necesita_el_relleno_se_hace_para_todas_las_empanadas_de_su_gusto()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo,
            Item(CarneSuave, 2, 13200m, pack: 12),
            Item(CarneSuave, 1, 7200m, pack: 6));

        var necesita = await new RecetaService(contexto).Necesita();

        // 30 empanadas contra un relleno que rinde 12: tres recetas de 600 g
        Assert.Equal(1800, necesita[Carne]);
    }

    [Fact]
    public async Task Necesita_lo_suelto_de_una_empanada_no_cuenta()
    {
        using var contexto = Armar();
        contexto.ProductoIngredientes.Add(new ProductoIngrediente { IdProducto = CarneSuave, IdIngrediente = Muzzarella, Cantidad = 50 });
        contexto.SaveChanges();
        Pedir(contexto, EstadoPedido.Nuevo, Item(CarneSuave, 1, 7200m, pack: 6));

        var necesita = await new RecetaService(contexto).Necesita();

        // todo lo que lleva una empanada viene en su relleno: un resto de cuando era otra cosa no se compra
        Assert.False(necesita.ContainsKey(Muzzarella));
    }

    // ---------- Desglose ----------

    [Fact]
    public async Task Desglose_el_ingrediente_de_una_receta_se_explica_por_la_receta()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var partes = await new RecetaService(contexto).Desglose(Harina);

        // dos renglones con el mismo total: desempata el nombre
        Assert.Equal(new[] { "Bollo de focaccia", "Bollo de pizza" }, partes.Select(x => x.Donde));
        Assert.All(partes, x => Assert.True(x.EsReceta));
        Assert.All(partes, x => Assert.Equal(1000, x.Total));

        var pizza = partes.Single(x => x.Donde == "Bollo de pizza");
        Assert.Equal(1000, pizza.Cantidad);
        Assert.Equal(6, pizza.Rinde);
        Assert.Equal(6, pizza.Piezas);   // 5 margaritas y 1 fugazzeta comparten el bollo
        Assert.Equal(1, pizza.Veces);
    }

    [Fact]
    public async Task Desglose_el_ingrediente_de_un_producto_va_por_producto_y_lo_que_mas_pesa_primero()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var partes = await new RecetaService(contexto).Desglose(Muzzarella);

        Assert.Equal(new[] { "Margarita", "Fugazzeta" }, partes.Select(x => x.Donde));
        Assert.Equal(new[] { 600m, 200m }, partes.Select(x => x.Total));
        Assert.All(partes, x => Assert.False(x.EsReceta));

        var margarita = partes[0];
        Assert.Equal(120, margarita.Cantidad);
        Assert.Equal(5, margarita.Piezas);
    }

    [Fact]
    public async Task Desglose_las_piezas_pedidas_sin_el_ingrediente_no_cuentan()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var albahaca = Assert.Single(await new RecetaService(contexto).Desglose(Albahaca));

        // 5 margaritas, pero 2 sin albahaca: la llevan 3
        Assert.Equal(3, albahaca.Piezas);
        Assert.Equal(12, albahaca.Total);
    }

    [Fact]
    public async Task Desglose_abierto_suma_lo_mismo_que_Necesita()
    {
        using var contexto = Armar();
        LaSemana(contexto);
        var servicio = new RecetaService(contexto);

        // es la misma cuenta mostrada de dos maneras: el total de la ficha y el número de la
        // grilla de ingredientes no pueden discrepar
        foreach (var (idIngrediente, total) in await servicio.Necesita())
        {
            Assert.Equal(total, (await servicio.Desglose(idIngrediente)).Sum(x => x.Total));
        }
    }

    [Fact]
    public async Task Desglose_de_algo_que_no_se_usa_esta_vacio()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        Assert.Empty(await new RecetaService(contexto).Desglose(idIngrediente: 999));
    }

    // ---------- FaltaComprar ----------

    [Fact]
    public async Task FaltaComprar_sin_pedidos_no_hay_nada_para_comprar()
    {
        using var contexto = Armar();

        Assert.Empty((await new RecetaService(contexto).FaltaComprar()).Renglones);
    }

    [Fact]
    public async Task FaltaComprar_es_lo_que_se_come_menos_lo_que_hay_con_lo_que_mas_falta_primero()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var renglones = (await new RecetaService(contexto).FaltaComprar()).Renglones;

        // Harina: se come 2000 y hay 5000, no falta. Agua: no se compra. Muzzarella: 800 - 200.
        // Albahaca: 12 - 3. Carne y muzzarella faltan lo mismo (600): desempata el nombre
        Assert.Equal(
            new[] { ("Tomate", "1 kg"), ("Carne", "600 g"), ("Muzzarella", "600 g"), ("Albahaca", "9 u"), ("Sal fina", "5 g") },
            renglones.Select(x => (x.Nombre, x.Cuanto)));
    }

    [Fact]
    public async Task FaltaComprar_lo_que_no_se_compra_no_se_manda_a_comprar()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var renglones = (await new RecetaService(contexto).FaltaComprar()).Renglones;

        // el agua se come (1350 ml) y no hay stock, pero es «libre»
        Assert.DoesNotContain(renglones, x => x.Nombre == "Agua");
    }

    [Fact]
    public async Task FaltaComprar_si_el_stock_justo_alcanza_no_falta()
    {
        using var contexto = Armar();
        LaSemana(contexto);
        contexto.Ingredientes.Single(x => x.IdIngrediente == Muzzarella).Stock = 800;
        contexto.SaveChanges();

        var renglones = (await new RecetaService(contexto).FaltaComprar()).Renglones;

        Assert.DoesNotContain(renglones, x => x.Nombre == "Muzzarella");
    }

    [Fact]
    public async Task FaltaComprar_con_todo_en_stock_la_lista_queda_vacia()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        foreach (var ingrediente in contexto.Ingredientes)
        {
            ingrediente.Stock = 100_000;
        }

        contexto.SaveChanges();

        Assert.Empty((await new RecetaService(contexto).FaltaComprar()).Renglones);
    }
}
