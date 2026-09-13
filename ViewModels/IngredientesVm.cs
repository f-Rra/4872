namespace f4872.ViewModels;

public class IngredientesVm : PanelVm
{
    public IReadOnlyList<FilaIngrediente> Lista { get; set; } = [];

    // La banda de arriba: para cuántos pedidos es esta cuenta y si alcanza.
    public int Pedidos { get; set; }
    public int Faltantes { get; set; }

    // Ver todos, o solo los que entran en algún pedido sin entregar. De fábrica
    // solo los del fin de semana: la pantalla es la lista de compras, no el
    // inventario, y de treinta y siete ingredientes la mayoría no se toca.
    public bool Todos { get; set; }

    public int Cuantos { get; set; }
    public int EnTotal { get; set; }

    // Los que estan en alguna receta. Distinto de EnTotal: uno recien dado de
    // alta todavia no esta en ninguna, y el subtitulo no puede contarlo.
    public int EnRecetas { get; set; }
}

public class FilaIngrediente
{
    public int IdIngrediente { get; set; }
    public string Nombre { get; set; } = null!;

    // ya formateados con su unidad: «2,5 kg», «300 g», «12 u». El stock viaja
    // así también para editarlo: el campo se escribe en las mismas palabras en
    // que se lee, y el servidor lo vuelve a leer con Cantidades.Leer
    public string Stock { get; set; } = null!;
    public string Necesito { get; set; } = null!;
    public string Falta { get; set; } = null!;

    // lo que no se compra —el agua, la masa madre— no tiene columna de falta
    public bool Libre { get; set; }

    // si hay que ir a buscarlo: el número de «Falta» es mayor que cero
    public bool HayQueComprar { get; set; }

    // «en 4 productos», «en bollo de masa», «todavía en ninguna receta»
    public string Donde { get; set; } = null!;
}
