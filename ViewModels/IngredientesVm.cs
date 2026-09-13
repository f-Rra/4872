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

    // De donde sale el numero que muestra la grilla, abierto por producto. Va
    // vacio cuando el ingrediente no entra en ningun pedido sin entregar.
    public IReadOnlyList<RenglonDesglose> Desglose { get; set; } = [];

    // la suma de los renglones de arriba, que es el «Necesito» de la grilla
    public string HaceFalta { get; set; } = "";

    // En cuantas recetas esta, contando las bases. Decide si se puede borrar:
    // sacarlo de abajo de una receta la dejaria rota, y la base lo prohibe con
    // un FK restrict. Se cuenta antes para poder decirlo con palabras.
    public int Usos { get; set; }

    // El alta es la misma ficha en blanco: no hay una pantalla de alta distinta
    // de la de edicion. Sin id todavia no existe.
    public bool EsNuevo => IdIngrediente == 0;

    public static readonly (Medida Valor, string Nombre)[] Medidas =
    [
        (Medida.Gramo, "Gramos"),
        (Medida.Mililitro, "Mililitros"),
        (Medida.Unidad, "Unidades")
    ];
}

// De dónde sale una parte de lo que hace falta: el renglón de un producto que
// lleva el ingrediente, o el de una base.
//
// Es lo que devuelve RecetaService y de lo que sale, sumado, el número de la
// grilla. No está formateado: la ficha decide cómo se lee cada renglón.
public class ParteDeReceta
{
    public int IdIngrediente { get; set; }

    // el nombre del producto, o el de la base: «Margarita», «Bollo de masa»
    public string Donde { get; set; } = null!;

    // Cuánto lleva. De un producto es por pieza; de una base es de la TANDA
    // ENTERA, que es como se carga la receta del bollo. Por eso viaja el rinde.
    public decimal Cantidad { get; set; }

    public int? Rinde { get; set; }

    // cuántas piezas lo llevan de verdad: las que lo pidieron sin no cuentan
    public int Piezas { get; set; }

    public decimal Total { get; set; }

    public bool EsBase => Rinde is not null;
}

// Un renglón del desglose, ya formateado. La vista no hace cuentas ni decide
// unidades: solo dibuja lo que le llega.
public class RenglonDesglose
{
    public string Donde { get; set; } = null!;

    // «rinde 6», solo en las bases
    public string? Rinde { get; set; }

    // «4 u», o «700 ml la tanda» si es una base
    public string Cuanto { get; set; } = null!;

    // «× 2 = 8 u», o el total pelado si es una base
    public string Sale { get; set; } = null!;
}
