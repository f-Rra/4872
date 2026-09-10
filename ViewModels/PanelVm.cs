namespace f4872.ViewModels;

// Lo que necesita el marco del panel: la barra lateral y la cabecera.
//
// Va en cada pantalla del panel porque el marco se dibuja en todas. Por eso son
// dos datos y no veinte: el globo de pedidos y el estado de la tienda se leen
// desde cualquier sección, y todo lo demás es de la sección que se esté viendo.
public class PanelVm
{
    public bool Abierta { get; set; } = true;

    // el globo al lado de «Pedidos»: los que están nuevos o preparándose
    public int SinEntregar { get; set; }
}

// El Inicio hereda el marco y le suma lo suyo. Cada sección va a hacer lo mismo:
// así el layout recibe siempre un PanelVm y no le importa qué pantalla es.
public class InicioVm : PanelVm
{
    // las cinco tarjetas del tablero, ya resueltas: la vista no calcula nada
    public IReadOnlyList<Cifra> Cifras { get; set; } = [];

    // qué hay que hornear, agrupado por familia
    public IReadOnlyList<GrupoHornear> Hornear { get; set; } = [];

    // qué falta comprar para poder hornearlo
    public ListaDeCompras Comprar { get; set; } = new();
}

public class ListaDeCompras
{
    public IReadOnlyList<RenglonComprar> Renglones { get; set; } = [];

    // cuántos ingredientes entran en algún pedido y no tienen ninguna medida
    // cargada: de esos no se puede decir ni que falta ni que alcanza
    public int SinMedida { get; set; }
}

public class RenglonComprar
{
    public string Nombre { get; set; } = null!;

    // ya formateado con su unidad: «2,5 kg», «300 g», «12 u»
    public string Cuanto { get; set; } = null!;

    // algún producto que lo lleva no tiene la medida cargada, así que el número
    // es un piso y no el total. Se marca con un asterisco
    public bool Flojo { get; set; }
}

// Una familia de la lista de hornear. Va agrupada y no en una lista sola porque
// son tres trabajos distintos: las pizzas y las focaccias se estiran y se
// hornean, las empanadas se arman.
public class GrupoHornear
{
    public string Familia { get; set; } = null!;

    // las empanadas se cuentan en unidades y llevan «u.» al lado del número;
    // las otras dos son piezas y no necesitan aclaración
    public bool PorUnidad { get; set; }

    public IReadOnlyList<RenglonHornear> Renglones { get; set; } = [];
}

public class RenglonHornear
{
    public string Nombre { get; set; } = null!;
    public int Cuantas { get; set; }
}

// Una tarjeta del tablero. Tres textos y nada más: el título, el número grande
// y el renglón chico que dice de qué es ese número. Van como texto y no como
// int o decimal porque algunas son plata, otras horas, y otras un guión cuando
// no hay nada que contar.
public class Cifra
{
    public string Titulo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string Nota { get; set; } = null!;
}
