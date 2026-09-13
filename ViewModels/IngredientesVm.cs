using f4872.Helpers;
using f4872.Models;

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

    // La ficha del ingrediente elegido. Nula con la grilla sola: la pantalla
    // funciona sin ella y el modal es una segunda capa, no el estado normal.
    public FichaIngrediente? Ficha { get; set; }

    // lo que salio mal al guardar, con el texto que va a leer una persona
    public string? Error { get; set; }

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

    // si hay que ir a buscarlo: el número de «Falta» es mayor que cero
    public bool HayQueComprar { get; set; }

    // «en 4 productos», «en bollo de masa», «todavía en ninguna receta»
    public string Donde { get; set; } = null!;
}


// Lo que se edita de un ingrediente: como se llama, en que se mide y como se
// compra. Las cantidades por producto no estan aca a proposito: se cargan en la
// receta de cada producto, que es donde se esta pensando en ese producto.
public class FichaIngrediente
{
    public int IdIngrediente { get; set; }

    public string Nombre { get; set; } = "";

    public Medida Unidad { get; set; }

    // Se compra o no. No es un campo: no hay perilla para tocarlo porque los
    // unicos dos que no se compran son el agua y la masa madre, y el resto se
    // compra todo. Viene de la base solo para saber si mostrar los dos campos.
    public bool Libre { get; set; }

    // Solo el numero: la unidad va escrita al lado de la pastilla y sale de la
    // medida elegida arriba. Sigue siendo texto y no decimal para poder devolver
    // lo tipeado tal cual cuando el guardado falla, y porque asi tambien entra
    // un «25 kg» si alguien lo escribe.
    public string Bulto { get; set; } = "";

    // numero pelado: el signo va afuera, como el precio de un producto
    public decimal? Precio { get; set; }

    // El encabezado sale del registro guardado y no de lo tipeado: es la
    // identidad de lo que estas editando, y al fallar el guardado con el nombre
    // vacio el titulo quedaba en blanco. Mismo motivo que en la ficha de producto.
    public string Titulo { get; set; } = "";
    public string Donde { get; set; } = "";

    // la que se dibuja al lado de la pastilla: g, ml o u
    public string UnidadCorta => Cantidades.Abreviatura(Unidad);

    public static readonly (Medida Valor, string Nombre)[] Medidas =
    [
        (Medida.Gramo, "Gramos"),
        (Medida.Mililitro, "Mililitros"),
        (Medida.Unidad, "Unidades")
    ];
}
