using f4872.Models;

namespace f4872.ViewModels;

public class CostosVm : PanelVm
{
    // "producto" o "mes", para marcar la solapa encendida
    public string Vista { get; set; } = "producto";

    public bool PorMes => Vista == "mes";

    public IReadOnlyList<CostoDeProducto> Lista { get; set; } = [];

    // la otra vista: un renglón por mes con pedidos entregados
    public IReadOnlyList<MesDeCostos> Meses { get; set; } = [];

    // el elegido de la derecha. Nulo solo si no hay ningún producto cargado
    public CostoDeProducto? Elegido { get; set; }

    // La banda de arriba: hacer todo lo que hay pedido cuesta tanto y se cobra
    // tanto. Es lo único de esta pantalla que mira los pedidos.
    public string Cuesta { get; set; } = "";
    public string SeCobra { get; set; } = "";
    public string Deja { get; set; } = "";
    public string? Margen { get; set; }

    // «2 productos bajo el 55%» o «3 sin terminar de cargar»
    public string Aparte { get; set; } = "";

    // La banda de la vista por mes: el mes que está corriendo, cuánto hay
    // todavía sin entregar, y el margen promedio de los meses ya cerrados.
    public MesDeCostos? EnCurso { get; set; }

    // nulo cuando no queda nada: «quedan $0 sin entregar» no dice nada
    public string? SinEntregarPlata { get; set; }
    public string? PromedioCerrados { get; set; }

    // Por debajo de esto el margen se marca flojo. Es una constante y no un
    // campo de la base por lo mismo que el texto de la tienda cerrada: la
    // pantalla de Configuración se descartó en el diseño, así que no hay dónde
    // editarlo. El día que haya, se muda.
    public const int MargenMinimo = 55;

    public static readonly (string Clave, string Nombre)[] Solapas =
    [
        ("producto", "Por producto"),
        ("mes", "Por mes")
    ];
}

// Lo que cuesta hacer una unidad y lo que deja.
//
// No depende de lo que haya pedido: es la receta contra el precio al que se
// compra cada ingrediente. Lo que sí depende de los pedidos es la banda.
public class CostoDeProducto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = null!;
    public Familia Familia { get; set; }

    public decimal Costo { get; set; }

    // Cuánto se cobra por una unidad. En las empanadas no es un precio propio:
    // sale del pack, dividido por sus unidades.
    public decimal Venta { get; set; }

    public decimal Deja => Venta - Costo;

    // nulo cuando no se puede calcular: sin precio de venta no hay porcentaje
    public int? Porcentaje => Venta > 0 ? (int)Math.Round(Deja / Venta * 100) : null;

    // Cuántos ingredientes de la receta no tienen precio de compra cargado. Con
    // alguno, el costo que se muestra es un piso y no el número real.
    public int SinPrecio { get; set; }

    public bool Flojo => SinPrecio == 0 && Porcentaje < CostosVm.MargenMinimo;

    public IReadOnlyList<RenglonDeCosto> Desglose { get; set; } = [];
}

// Un renglón del desglose. Los de la base van sangrados y no suman: ya están
// contados en el renglón de la base, que lleva el total de una unidad.
public class RenglonDeCosto
{
    public string Nombre { get; set; } = null!;

    // «120 g». Vacío en el renglón de la base, que no lleva una cantidad suya
    public string Cuanto { get; set; } = "";

    public decimal? Sale { get; set; }

    // el renglón de la base, el que sí suma
    public bool EsBase { get; set; }

    // los ingredientes de la base, que cuelgan del anterior
    public bool DeLaBase { get; set; }
}

// Un mes del historial: lo que se cobró contra lo que costó.
//
// Lo cobrado es histórico de verdad, porque el precio se copia al item cuando
// se confirma el pedido. El costo sale de los precios de compra de hoy: el
// ingrediente guarda un precio, no una historia.
public class MesDeCostos
{
    // el primero del mes, para ordenar y para nombrarlo
    public DateOnly Mes { get; set; }

    public int Pedidos { get; set; }
    public decimal Cobro { get; set; }
    public decimal Costo { get; set; }

    public decimal Deja => Cobro - Costo;

    public int? Porcentaje => Cobro > 0 ? (int)Math.Round(Deja / Cobro * 100) : null;

    // el mes en curso: su margen todavía se mueve, así que no entra en el
    // promedio de los cerrados
    public bool Abierto { get; set; }
}
