using f4872.Models;

namespace f4872.ViewModels;

public class ProduccionVm : PanelVm
{
    public IReadOnlyList<Titular> Titulares { get; set; } = [];
    public IReadOnlyList<GrupoProduccion> Grupos { get; set; } = [];
}

// una de las tres cifras grandes de arriba. Misma forma que la Cifra del Inicio
// pero con su propia clase: si mañana una de las dos cambia, no arrastra a la otra
public class Titular
{
    public string Titulo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string Nota { get; set; } = null!;
}

public class GrupoProduccion
{
    public string Familia { get; set; } = null!;

    // el total de la familia, al lado del título: «Pizzas · 20»
    public int Total { get; set; }

    public IReadOnlyList<RenglonProduccion> Renglones { get; set; } = [];
}

// una variante de un producto: qué se le saca y cuántas van así
public class Combinacion
{
    public string Como { get; set; } = null!;
    public int Piezas { get; set; }
}

public class RenglonProduccion
{
    public int Cuantas { get; set; }
    public string Nombre { get; set; } = null!;

    // «2 sin albahaca» en pizzas y focaccias, «1 pack de 12» en empanadas
    public IReadOnlyList<string> Detalle { get; set; } = [];

    // Los «sin» van en pastilla y los packs no. No es decoración: un «sin» es
    // una instrucción de armado que hay que ver de lejos mientras se trabaja;
    // el desglose de packs es solo de dónde salió el número.
    public bool EnPastilla { get; set; }
}

// Lo que hay que hacer, consolidado. Lo usan el Inicio y Producción: el Inicio
// muestra solo los totales y Producción abre el desglose.
public class Consolidado
{
    public IReadOnlyList<ProductoPedido> Productos { get; set; } = [];

    // los bollos son las piezas de pizza y focaccia: una pieza, un bollo
    public int Bollos => Productos.Where(x => x.Familia != Familia.Empanada).Sum(x => x.Piezas);

    public int Empanadas => Productos.Where(x => x.Familia == Familia.Empanada).Sum(x => x.Piezas);

    public int Gustos => Productos.Count(x => x.Familia == Familia.Empanada);
}

public class ProductoPedido
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = null!;
    public Familia Familia { get; set; }

    // en empanadas son unidades, no packs: lo que se cocina
    public int Piezas { get; set; }

    // Las variantes que hay que armar, y cuántas piezas de cada una. Las que van
    // enteras no están: salen por resta contra el total del renglón.
    //
    // Van por combinación y no por ingrediente. Contar por ingrediente da un
    // número que no se puede usar: si de cinco margaritas dos van sin albahaca y
    // oliva y una sin albahaca, «3 sin albahaca · 2 sin oliva» cuenta dos pizzas
    // dos veces. Acá cada renglón es una pizza distinta de verdad.
    public IReadOnlyList<Combinacion> Combinaciones { get; set; } = [];

    // unidades del pack → cuántos packs. Vacío en pizzas y focaccias
    public IReadOnlyDictionary<int, int> Packs { get; set; } = new Dictionary<int, int>();
}
