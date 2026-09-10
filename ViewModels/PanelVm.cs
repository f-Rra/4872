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
