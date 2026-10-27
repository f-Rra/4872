using f4872.Models;
using f4872.Services;
using f4872.ViewModels;
using static f4872.Tests.Cocina;

namespace f4872.Tests;

// Qué hay que hacer el fin de semana: lo pedido sumado por producto (Consolidar), cuántas recetas
// de masa salen de ahí (Amasado) y cuánto pesa un bollo (PesoDelBollo). Es lo que lee el vendedor
// con las manos en la harina, así que un número de más o de menos se paga en la mesada.
public class ProduccionTests
{
    private static ProductoPedido Pedido(int id, string nombre, Familia familia, int piezas) =>
        new() { IdProducto = id, Nombre = nombre, Familia = familia, Piezas = piezas };

    // ---------- Consolidar ----------

    [Fact]
    public async Task Consolidar_sin_pedidos_no_hay_nada_que_hacer()
    {
        using var contexto = Armar();

        var consolidado = await new RecetaService(contexto).Consolidar();

        Assert.Empty(consolidado.Productos);
    }

    [Fact]
    public async Task Consolidar_junta_los_renglones_de_cada_producto_y_en_el_orden_de_la_carta()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var productos = (await new RecetaService(contexto).Consolidar()).Productos;

        // pizzas, focaccias y empanadas, y dentro de cada familia por posición. Las 10 margaritas
        // entregadas y las 10 canceladas no cuentan: las 5 son las de los pedidos que siguen abiertos
        Assert.Equal(new[] { Margarita, Fugazzeta, Romero, CarneSuave }, productos.Select(x => x.IdProducto));
        Assert.Equal(new[] { 5, 1, 1, 12 }, productos.Select(x => x.Piezas));
    }

    [Fact]
    public async Task Consolidar_nombra_solo_las_variantes_con_algo_sacado()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var productos = (await new RecetaService(contexto).Consolidar()).Productos;

        // de las 5 margaritas, 2 van sin albahaca; las otras 3 salen por resta y no se nombran
        var margarita = productos.Single(x => x.IdProducto == Margarita);
        var variante = Assert.Single(margarita.Combinaciones);
        Assert.Equal("sin albahaca", variante.Como);
        Assert.Equal(2, variante.Piezas);

        Assert.Empty(productos.Single(x => x.IdProducto == Fugazzeta).Combinaciones);
    }

    [Fact]
    public async Task Consolidar_la_misma_combinacion_en_otro_orden_es_una_sola_pizza()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo,
            Item(Margarita, 1, PrecioMargarita, null, "Oliva", "Albahaca"),
            Item(Margarita, 1, PrecioMargarita, null, "Albahaca", "Oliva"),
            Item(Margarita, 3, PrecioMargarita, null, "Oliva"),
            Item(Margarita, 1, PrecioMargarita, null, "Albahaca"));

        var margarita = (await new RecetaService(contexto).Consolidar()).Productos.Single();

        // las de mayor cantidad primero: es el orden en que se arman. Las 6 piezas están repartidas
        // en renglones que no se pisan: 3 + 2 + 1
        Assert.Equal(6, margarita.Piezas);
        Assert.Equal(new[] { "sin oliva", "sin albahaca, oliva", "sin albahaca" }, margarita.Combinaciones.Select(x => x.Como));
        Assert.Equal(new[] { 3, 2, 1 }, margarita.Combinaciones.Select(x => x.Piezas));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consolidar_con_igual_cantidad_las_variantes_van_por_nombre(bool alReves)
    {
        using var contexto = Armar();

        // se prueba con las dos órdenes de llegada: el resultado no puede depender de cuál pidió primero
        var sinOliva = Item(Fugazzeta, 1, PrecioFugazzeta, null, "Oliva");
        var sinAlbahaca = Item(Fugazzeta, 1, PrecioFugazzeta, null, "Albahaca");
        Pedir(contexto, EstadoPedido.Nuevo, alReves ? [sinAlbahaca, sinOliva] : [sinOliva, sinAlbahaca]);

        var fugazzeta = (await new RecetaService(contexto).Consolidar()).Productos.Single();

        Assert.Equal(new[] { "sin albahaca", "sin oliva" }, fugazzeta.Combinaciones.Select(x => x.Como));
    }

    [Fact]
    public async Task Consolidar_una_empanada_por_pack_se_cuenta_en_unidades()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo, Item(CarneSuave, 2, 7200m, pack: 6));
        Pedir(contexto, EstadoPedido.Preparando,
            Item(CarneSuave, 1, 13200m, pack: 12),
            Item(CarneSuave, 1, 7200m, pack: 6));

        var empanada = (await new RecetaService(contexto).Consolidar()).Productos.Single();

        // 3 packs de 6 y uno de 12: 30 empanadas. Se cocinan unidades, no packs
        Assert.Equal(30, empanada.Piezas);
        Assert.Equal(new Dictionary<int, int> { [6] = 3, [12] = 1 }, empanada.Packs);
    }

    [Fact]
    public async Task Consolidar_pizzas_y_focaccias_no_tienen_packs()
    {
        using var contexto = Armar();
        LaSemana(contexto);

        var productos = (await new RecetaService(contexto).Consolidar()).Productos;

        Assert.All(productos.Where(x => x.Familia != Familia.Empanada), x => Assert.Empty(x.Packs));
        Assert.Equal(new Dictionary<int, int> { [12] = 1 }, productos.Single(x => x.IdProducto == CarneSuave).Packs);
    }

    [Theory]
    [InlineData(EstadoPedido.Nuevo, 4)]
    [InlineData(EstadoPedido.Preparando, 4)]
    [InlineData(EstadoPedido.Entregado, 0)]
    [InlineData(EstadoPedido.Cancelado, 0)]
    public async Task Consolidar_solo_cuenta_los_pedidos_que_siguen_abiertos(EstadoPedido estado, int piezas)
    {
        using var contexto = Armar();
        Pedir(contexto, estado, Item(Margarita, 4, PrecioMargarita));

        var productos = (await new RecetaService(contexto).Consolidar()).Productos;

        Assert.Equal(piezas, productos.Sum(x => x.Piezas));
    }

    // ---------- Amasado ----------

    [Fact]
    public async Task Amasado_cada_masa_es_una_receta_aparte()
    {
        using var contexto = Armar();
        LaSemana(contexto);
        var servicio = new RecetaService(contexto);

        var texto = await servicio.Amasado((await servicio.Consolidar()).Productos);

        // 6 pizzas son una receta de pizza (rinde 6) y 1 focaccia es una de focaccia (rinde 4).
        // Con el mismo número de recetas, la focaccia sale antes por orden alfabético
        Assert.Equal("1 receta de focaccia · 1 de pizza", texto);
    }

    [Fact]
    public async Task Amasado_redondea_para_arriba_porque_media_receta_no_se_amasa()
    {
        using var contexto = Armar();

        var texto = await new RecetaService(contexto).Amasado([Pedido(Margarita, "Margarita", Familia.Pizza, 13)]);

        // 13 / 6 = 2,17: tres veces
        Assert.Equal("3 recetas de pizza", texto);
    }

    [Fact]
    public async Task Amasado_la_que_mas_veces_se_hace_va_primera()
    {
        using var contexto = Armar();

        var texto = await new RecetaService(contexto).Amasado(
        [
            Pedido(Romero, "Romero y sal", Familia.Focaccia, 1),
            Pedido(Margarita, "Margarita", Familia.Pizza, 13)
        ]);

        // la palabra «recetas» va una sola vez, en la primera
        Assert.Equal("3 recetas de pizza · 1 de focaccia", texto);
    }

    [Fact]
    public async Task Amasado_las_pizzas_que_comparten_bollo_se_amasan_juntas()
    {
        using var contexto = Armar();
        var servicio = new RecetaService(contexto);

        // 4 margaritas y 3 fugazzetas son 7 bollos de la misma masa: dos recetas, no una y una
        var texto = await servicio.Amasado(
        [
            Pedido(Margarita, "Margarita", Familia.Pizza, 4),
            Pedido(Fugazzeta, "Fugazzeta", Familia.Pizza, 3)
        ]);

        Assert.Equal("2 recetas de pizza", texto);
    }

    [Fact]
    public async Task Amasado_dos_masas_que_rinden_lo_mismo_siguen_siendo_dos_masas()
    {
        using var contexto = Armar();
        contexto.Recetas.Single(x => x.IdReceta == BaseFocaccia).Rinde = 6;
        contexto.SaveChanges();

        var texto = await new RecetaService(contexto).Amasado(
        [
            Pedido(Margarita, "Margarita", Familia.Pizza, 6),
            Pedido(Romero, "Romero y sal", Familia.Focaccia, 6)
        ]);

        // las dos rinden seis, pero juntarlas mandaría a amasar «2 recetas de pizza» con una sola harina
        Assert.Equal("1 receta de focaccia · 1 de pizza", texto);
    }

    [Fact]
    public async Task Amasado_las_empanadas_no_llevan_masa_de_bollo()
    {
        using var contexto = Armar();
        var servicio = new RecetaService(contexto);

        Assert.Equal("nada que amasar", await servicio.Amasado([Pedido(CarneSuave, "Carne suave", Familia.Empanada, 24)]));
        Assert.Equal("nada que amasar", await servicio.Amasado([]));
    }

    [Fact]
    public async Task Amasado_un_producto_sin_base_cargada_lo_dice()
    {
        using var contexto = Armar();
        contexto.Productos.Add(new Producto { IdProducto = 9, Familia = Familia.Pizza, Nombre = "Sin masa", Precio = 5000m });
        contexto.SaveChanges();

        var texto = await new RecetaService(contexto).Amasado([Pedido(9, "Sin masa", Familia.Pizza, 2)]);

        Assert.Equal("sin base cargada", texto);
    }

    [Fact]
    public async Task Amasado_un_nombre_que_no_empieza_con_bollo_se_deja_entero()
    {
        using var contexto = Armar();
        contexto.Recetas.Single(x => x.IdReceta == BasePizza).Nombre = "Masa Integral";
        contexto.SaveChanges();

        var texto = await new RecetaService(contexto).Amasado([Pedido(Margarita, "Margarita", Familia.Pizza, 1)]);

        // no sabe cómo acortar un nombre que no conoce: lo único honesto es no tocarlo
        Assert.Equal("1 receta de masa integral", texto);
    }

    // ---------- PesoDelBollo ----------

    [Fact]
    public async Task PesoDelBollo_es_lo_que_pesa_la_receta_dividido_por_lo_que_rinde()
    {
        using var contexto = Armar();
        var servicio = new RecetaService(contexto);

        // pizza: (1000 + 600) / 6. Focaccia: (1000 + 750) / 4
        Igual(1600m / 6, await servicio.PesoDelBollo(Familia.Pizza));
        Igual(437.5m, await servicio.PesoDelBollo(Familia.Focaccia));
    }

    [Fact]
    public async Task PesoDelBollo_lo_que_se_mide_en_unidades_no_pesa()
    {
        using var contexto = Armar();
        contexto.RecetaIngredientes.Add(new RecetaIngrediente { IdReceta = BasePizza, IdIngrediente = Albahaca, Cantidad = 6 });
        contexto.SaveChanges();

        // seis hojas de albahaca no son seis gramos: se dejan afuera
        Igual(1600m / 6, await new RecetaService(contexto).PesoDelBollo(Familia.Pizza));
    }

    [Fact]
    public async Task PesoDelBollo_sin_masa_cargada_no_hay_bollo_que_decir()
    {
        using var contexto = Armar();

        // la empanada no tiene bollo
        Assert.Null(await new RecetaService(contexto).PesoDelBollo(Familia.Empanada));
    }
}
