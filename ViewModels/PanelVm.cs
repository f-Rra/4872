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
