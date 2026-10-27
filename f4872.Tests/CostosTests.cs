using f4872.Helpers;
using f4872.Models;
using f4872.Services;
using static f4872.Tests.Cocina;

namespace f4872.Tests;

// Cuánto cuesta hacer cada cosa y cuánto deja: Costos (por producto), CostoDeRecetas (por receta) y
// PorMes (lo cobrado contra lo que costó, de los pedidos entregados).
//
// Con la cocina de prueba, lo que lleva una unidad sale así (precios en el encabezado de Cocina):
//
//   Margarita    bollo 250 + salsa 200 + muzzarella 960 + albahaca 20   = 1430   se vende a 9800
//   Fugazzeta    bollo 250 + muzzarella 1600                            = 1850   se vende a 11200
//   Romero       bollo 375 + sal 5                                      =  380   se vende a 6800
//   Carne suave  relleno 600 g / 12 × $10                               =  500   se vende a 1100 (pack de 12)
public class CostosTests
{
    // ---------- Costos ----------

    [Fact]
    public async Task Costos_cada_receta_se_divide_por_su_rinde_y_lo_suelto_se_suma_entero()
    {
        using var contexto = Armar();

        var costos = await new RecetaService(contexto).Costos();

        Igual(1430m, costos.Single(x => x.IdProducto == Margarita).Costo);
        Igual(1850m, costos.Single(x => x.IdProducto == Fugazzeta).Costo);
        Igual(380m, costos.Single(x => x.IdProducto == Romero).Costo);
        Igual(500m, costos.Single(x => x.IdProducto == CarneSuave).Costo);
    }

    [Fact]
    public async Task Costos_lo_que_deja_y_el_porcentaje()
    {
        using var contexto = Armar();

        var margarita = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == Margarita);

        Assert.Equal(PrecioMargarita, margarita.Venta);
        Igual(8370m, margarita.Deja);
        Assert.Equal(85, margarita.Porcentaje);   // 8370 / 9800 = 85,4
        Assert.Equal(0, margarita.SinPrecio);
        Assert.False(margarita.Flojo);
    }

    [Fact]
    public async Task Costos_el_peor_margen_va_primero()
    {
        using var contexto = Armar();

        var costos = await new RecetaService(contexto).Costos();

        // 55 %, 83 %, 85 % y 94 %: es la pantalla de qué revisar
        Assert.Equal(new[] { CarneSuave, Fugazzeta, Margarita, Romero }, costos.Select(x => x.IdProducto));
        Assert.Equal(new int?[] { 55, 83, 85, 94 }, costos.Select(x => x.Porcentaje));
    }

    [Fact]
    public async Task Costos_la_empanada_se_vende_al_pack_mas_barato_por_unidad()
    {
        using var contexto = Armar();

        var empanada = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == CarneSuave);

        // el pack de 6 sale a 1200 la unidad y el de 12 a 1100. El de 24, apagado, saldría a 833 y no cuenta
        Igual(1100m, empanada.Venta);
    }

    [Fact]
    public async Task Costos_sin_ningun_pack_activo_la_empanada_no_tiene_precio()
    {
        using var contexto = Armar();
        foreach (var pack in contexto.Packs)
        {
            pack.Activo = false;
        }

        contexto.SaveChanges();

        var costos = await new RecetaService(contexto).Costos();
        var empanada = costos.Single(x => x.IdProducto == CarneSuave);

        // sin precio de venta no hay porcentaje, y lo que no se puede calcular va primero
        Assert.Equal(0m, empanada.Venta);
        Assert.Null(empanada.Porcentaje);
        Assert.Equal(CarneSuave, costos[0].IdProducto);
    }

    [Fact]
    public async Task Costos_un_producto_sin_precio_de_venta_no_tiene_porcentaje()
    {
        using var contexto = Armar();
        contexto.Productos.Single(x => x.IdProducto == Romero).Precio = null;
        contexto.SaveChanges();

        var romero = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == Romero);

        Assert.Null(romero.Porcentaje);
        Assert.False(romero.Flojo);
    }

    [Theory]
    [InlineData(9900, 55, false)]    // costo 495: deja justo el 55 %, que es el mínimo
    [InlineData(10000, 55, false)]   // costo 500: 54,5 se redondea a 55
    [InlineData(10100, 54, true)]    // costo 505: 54,1 queda abajo
    [InlineData(11000, 50, true)]    // costo 550
    public async Task Costos_el_margen_es_flojo_por_debajo_del_55(decimal precioDelKilo, int porcentaje, bool flojo)
    {
        using var contexto = Armar();
        contexto.Ingredientes.Single(x => x.IdIngrediente == Carne).PrecioDeCompra = precioDelKilo;
        contexto.SaveChanges();

        var empanada = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == CarneSuave);

        Assert.Equal(porcentaje, empanada.Porcentaje);
        Assert.Equal(flojo, empanada.Flojo);
    }

    [Fact]
    public async Task Costos_un_ingrediente_sin_precio_deja_un_piso_y_avisa_en_vez_de_marcar_flojo()
    {
        using var contexto = Armar();
        contexto.Ingredientes.Single(x => x.IdIngrediente == Tomate).PrecioDeCompra = null;
        contexto.SaveChanges();

        var costos = await new RecetaService(contexto).Costos();
        var margarita = costos.Single(x => x.IdProducto == Margarita);

        // la salsa cuenta cero: el costo es un piso (1430 - 200), y se avisa que falta un precio
        Igual(1230m, margarita.Costo);
        Assert.Equal(1, margarita.SinPrecio);

        // el resto de la carta no se entera
        Assert.All(costos.Where(x => x.IdProducto != Margarita), x => Assert.Equal(0, x.SinPrecio));
    }

    [Fact]
    public async Task Costos_con_un_precio_sin_cargar_el_margen_bajo_no_se_marca_flojo()
    {
        using var contexto = Armar();
        contexto.Ingredientes.Single(x => x.IdIngrediente == Tomate).PrecioDeCompra = null;
        contexto.Productos.Single(x => x.IdProducto == Margarita).Precio = 1500m;
        contexto.SaveChanges();

        var margarita = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == Margarita);

        // el costo es un piso y el margen real es todavía peor: pero marcarlo «flojo» sería afirmar
        // un número que no se sabe. Primero hay que cargar el precio que falta
        Assert.Equal(18, margarita.Porcentaje);   // (1500 - 1230) / 1500
        Assert.Equal(1, margarita.SinPrecio);
        Assert.False(margarita.Flojo);
    }

    [Fact]
    public async Task Costos_lo_libre_cuesta_cero_y_no_es_un_precio_que_falta()
    {
        using var contexto = Armar();

        var costos = await new RecetaService(contexto).Costos();

        // el agua no tiene precio de compra y está en los bollos, pero no se compra: no falta nada
        Assert.All(costos, x => Assert.Equal(0, x.SinPrecio));
    }

    [Fact]
    public async Task Costos_media_porcion_de_salsa_cuesta_la_mitad()
    {
        using var contexto = Armar();
        contexto.ProductoSalsas.Single().Porciones = 0.5m;
        contexto.SaveChanges();

        var margarita = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == Margarita);

        Igual(1330m, margarita.Costo);   // la salsa pasa de 200 a 100
    }

    [Fact]
    public async Task Costos_lo_suelto_de_una_empanada_no_cuenta()
    {
        using var contexto = Armar();
        contexto.ProductoIngredientes.Add(new ProductoIngrediente { IdProducto = CarneSuave, IdIngrediente = Muzzarella, Cantidad = 50 });
        contexto.SaveChanges();

        var empanada = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == CarneSuave);

        Igual(500m, empanada.Costo);
        Assert.DoesNotContain(empanada.Desglose, x => x.Nombre == "Muzzarella");
    }

    [Fact]
    public async Task Costos_el_desglose_pone_cada_receta_con_sus_ingredientes_colgando()
    {
        using var contexto = Armar();

        var margarita = (await new RecetaService(contexto).Costos()).Single(x => x.IdProducto == Margarita);

        // primero las recetas que usa (la base y la salsa), cada una con lo suyo; después lo que lleva arriba
        Assert.Equal(
            new[] { "Bollo de pizza", "Harina 00", "Agua", "Salsa de tomate", "Tomate", "Muzzarella", "Albahaca" },
            margarita.Desglose.Select(x => x.Nombre));
        Assert.Equal(
            new[] { true, false, false, true, false, false, false },
            margarita.Desglose.Select(x => x.EsReceta));
        Assert.Equal(
            new[] { false, true, true, false, true, false, false },
            margarita.Desglose.Select(x => x.DeLaReceta));

        // el renglón de la receta lleva el total de una unidad y los de abajo no se vuelven a sumar
        var recetas = margarita.Desglose.Where(x => x.EsReceta).Sum(x => x.Sale);
        var sueltos = margarita.Desglose.Where(x => !x.EsReceta && !x.DeLaReceta).Sum(x => x.Sale);
        Igual(margarita.Costo, recetas + sueltos);

        // lo que se lee en la pantalla: «120 g», «4 u»
        Assert.Equal("120 g", margarita.Desglose.Single(x => x.Nombre == "Muzzarella").Cuanto);
        Assert.Equal("4 u", margarita.Desglose.Single(x => x.Nombre == "Albahaca").Cuanto);
    }

    [Fact]
    public async Task Costos_no_depende_de_lo_que_haya_pedido()
    {
        using var contexto = Armar();
        var antes = (await new RecetaService(contexto).Costos()).Select(x => (x.IdProducto, x.Costo)).ToList();

        LaSemana(contexto);

        var despues = (await new RecetaService(contexto).Costos()).Select(x => (x.IdProducto, x.Costo)).ToList();
        Assert.Equal(antes, despues);
    }

    // ---------- CostoDeRecetas ----------

    [Fact]
    public async Task CostoDeRecetas_una_pieza_y_la_receta_entera()
    {
        using var contexto = Armar();

        var costos = await new RecetaService(contexto).CostoDeRecetas();

        Igual(250m, costos[BasePizza].PorPieza);       // 1000 g / 6 × $1,5
        Igual(1500m, costos[BasePizza].Entera);
        Igual(375m, costos[BaseFocaccia].PorPieza);    // 1000 g / 4 × $1,5
        Igual(200m, costos[SalsaDeTomate].PorPieza);   // 1000 g / 10 × $2
        Igual(2000m, costos[SalsaDeTomate].Entera);
        Igual(500m, costos[RellenoDeCarne].PorPieza);  // 600 g / 12 × $10
        Igual(6000m, costos[RellenoDeCarne].Entera);
        Assert.All(costos.Values, x => Assert.Empty(x.SinPrecio));
    }

    [Fact]
    public async Task CostoDeRecetas_dice_cuales_ingredientes_no_tienen_precio_por_nombre()
    {
        using var contexto = Armar();
        contexto.Ingredientes.Single(x => x.IdIngrediente == Tomate).PrecioDeCompra = null;
        contexto.Ingredientes.Single(x => x.IdIngrediente == Sal).PrecioDeCompra = null;
        contexto.RecetaIngredientes.Add(new RecetaIngrediente { IdReceta = SalsaDeTomate, IdIngrediente = Sal, Cantidad = 10 });
        contexto.SaveChanges();

        var salsa = (await new RecetaService(contexto).CostoDeRecetas())[SalsaDeTomate];

        Assert.Equal(new[] { "Sal fina", "Tomate" }, salsa.SinPrecio);
        Assert.Equal(0m, salsa.PorPieza);
    }

    [Fact]
    public async Task CostoDeRecetas_el_bollo_cuesta_lo_mismo_en_Recetas_y_en_Costos()
    {
        using var contexto = Armar();
        var servicio = new RecetaService(contexto);

        var enRecetas = (await servicio.CostoDeRecetas())[BasePizza].PorPieza;
        var margarita = (await servicio.Costos()).Single(x => x.IdProducto == Margarita);

        // las dos pantallas cuentan igual para no poder decir números distintos
        Igual(enRecetas, margarita.Desglose.Single(x => x.Nombre == "Bollo de pizza").Sale);
    }

    // ---------- PorMes ----------

    [Fact]
    public async Task PorMes_sin_pedidos_entregados_no_hay_meses()
    {
        using var contexto = Armar();
        Pedir(contexto, EstadoPedido.Nuevo, Item(Romero, 1, PrecioRomero));
        Pedir(contexto, EstadoPedido.Cancelado, Item(Romero, 1, PrecioRomero));

        Assert.Empty(await new RecetaService(contexto).PorMes());
    }

    [Fact]
    public async Task PorMes_lo_cobrado_es_el_precio_de_ese_dia_y_el_costo_el_de_hoy()
    {
        using var contexto = Armar();
        // se vendió a 9000 cuando hoy cuesta 9800: lo cobrado no se reescribe
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc),
            Item(Margarita, 2, 9000m));

        var mes = Assert.Single(await new RecetaService(contexto).PorMes());

        Assert.Equal(new DateOnly(2026, 8, 1), mes.Mes);
        Assert.Equal(1, mes.Pedidos);
        Assert.Equal(18000m, mes.Cobro);
        Igual(2860m, mes.Costo);   // 2 × 1430
        Assert.Equal(84, mes.Porcentaje);   // 15140 / 18000
        Assert.False(mes.Abierto);
    }

    [Fact]
    public async Task PorMes_si_cambia_un_precio_de_compra_un_mes_viejo_se_recostea()
    {
        using var contexto = Armar();
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc),
            Item(Margarita, 2, 9000m));
        var servicio = new RecetaService(contexto);

        Igual(2860m, (await servicio.PorMes()).Single().Costo);

        // la muzzarella sube al doble: la margarita pasa de 1430 a 2390
        contexto.Ingredientes.Single(x => x.IdIngrediente == Muzzarella).PrecioDeCompra = 16000;
        contexto.SaveChanges();

        var mes = (await servicio.PorMes()).Single();
        Igual(4780m, mes.Costo);
        Assert.Equal(18000m, mes.Cobro);
    }

    [Fact]
    public async Task PorMes_la_empanada_cobra_el_pack_y_cuesta_por_unidad()
    {
        using var contexto = Armar();
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc),
            Item(CarneSuave, 1, 13200m, pack: 12));

        var mes = (await new RecetaService(contexto).PorMes()).Single();

        Assert.Equal(13200m, mes.Cobro);
        Igual(6000m, mes.Costo);   // 12 empanadas × 500
    }

    [Fact]
    public async Task PorMes_el_mes_es_el_de_Buenos_Aires_y_no_el_de_UTC()
    {
        using var contexto = Armar();
        // 01:30 UTC del 1 de agosto son las 22:30 del 31 de julio en Buenos Aires
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 1, 1, 30, 0, DateTimeKind.Utc), Item(Romero, 1, 6000m));
        // 03:00 UTC es la medianoche justa: ya es agosto
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc), Item(Romero, 1, 6000m));

        var meses = await new RecetaService(contexto).PorMes();

        Assert.Equal(new[] { new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1) }, meses.Select(x => x.Mes));
        Assert.All(meses, x => Assert.Equal(1, x.Pedidos));
    }

    [Fact]
    public async Task PorMes_va_del_mas_viejo_al_mas_nuevo_y_agrupa_por_mes_y_año()
    {
        using var contexto = Armar();
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc), Item(Romero, 1, 6000m));
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc), Item(Romero, 2, 6000m));
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2026, 7, 5, 15, 0, 0, DateTimeKind.Utc), Item(Romero, 1, 6000m));
        PedirEl(contexto, EstadoPedido.Entregado, new DateTime(2025, 12, 5, 15, 0, 0, DateTimeKind.Utc), Item(Romero, 1, 6000m));

        var meses = await new RecetaService(contexto).PorMes();

        Assert.Equal(
            new[] { new DateOnly(2025, 12, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1) },
            meses.Select(x => x.Mes));
        Assert.Equal(new[] { 1, 1, 2 }, meses.Select(x => x.Pedidos));
        Assert.Equal(new[] { 6000m, 6000m, 18000m }, meses.Select(x => x.Cobro));
    }

    [Fact]
    public async Task PorMes_solo_cuentan_los_entregados()
    {
        using var contexto = Armar();
        var fecha = new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc);
        PedirEl(contexto, EstadoPedido.Entregado, fecha, Item(Romero, 1, 6000m));
        PedirEl(contexto, EstadoPedido.Nuevo, fecha, Item(Romero, 5, 6000m));
        PedirEl(contexto, EstadoPedido.Preparando, fecha, Item(Romero, 5, 6000m));
        PedirEl(contexto, EstadoPedido.Cancelado, fecha, Item(Romero, 5, 6000m));

        var mes = Assert.Single(await new RecetaService(contexto).PorMes());

        Assert.Equal(1, mes.Pedidos);
        Assert.Equal(6000m, mes.Cobro);
    }

    [Fact]
    public async Task PorMes_el_mes_en_curso_esta_abierto_y_los_anteriores_no()
    {
        using var contexto = Armar();
        var ahora = DateTime.UtcNow;
        PedirEl(contexto, EstadoPedido.Entregado, ahora, Item(Romero, 1, 6800m));
        PedirEl(contexto, EstadoPedido.Entregado, ahora.AddYears(-1), Item(Romero, 1, 6800m));

        var meses = await new RecetaService(contexto).PorMes();

        var local = Reloj.EnBuenosAires(ahora);
        Assert.Equal(new DateOnly(local.Year, local.Month, 1), meses[^1].Mes);
        Assert.True(meses[^1].Abierto);
        Assert.False(meses[0].Abierto);
    }
}
