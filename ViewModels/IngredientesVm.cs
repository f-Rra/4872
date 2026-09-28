using f4872.Helpers;
using f4872.Models;

namespace f4872.ViewModels;

public class IngredientesVm : PanelVm
{
    public IReadOnlyList<FilaIngrediente> Lista { get; set; } = [];

    // La banda de arriba: para cuántos pedidos es esta cuenta y si alcanza.
    public int Pedidos { get; set; }
    public int Faltantes { get; set; }

    // La ficha del ingrediente elegido. Nula con la grilla sola: la pantalla
    // funciona sin ella y el modal es una segunda capa, no el estado normal.
    public FichaIngrediente? Ficha { get; set; }

    // lo que salio mal al guardar, con el texto que va a leer una persona
    public string? Error { get; set; }

    public int EnTotal { get; set; }

    // Los que estan en alguna receta. Distinto de EnTotal: uno recien dado de
    // alta todavia no esta en ninguna, y el subtitulo no puede contarlo.
    public int EnRecetas { get; set; }

    // "todo" o el nombre de un rubro, para marcar el chip encendido
    public string Rubro { get; set; } = "todo";

    // Los cuatro, en el orden en que se muestran, con el nombre que se lee.
    public static readonly (Models.Rubro Valor, string Nombre)[] Rubros =
    [
        (Models.Rubro.QuesosYFiambres, "Quesos & Fiambres"),
        (Models.Rubro.Almacen, "Almacén"),
        (Models.Rubro.Carnes, "Carnes"),
        (Models.Rubro.Vegetales, "Vegetales")
    ];

    public static readonly (string Clave, string Nombre)[] Chips =
        [("todo", "Todo"), .. Rubros.Select(x => (x.Valor.ToString(), x.Nombre))];

    // Los grupos de la grilla, en el orden en que se muestran. Primero los que
    // todavía no tienen rubro, porque falta elegírselo; después los cuatro, y al
    // final lo que no se compra, que no tiene dónde. Adentro de cada uno sigue
    // el orden de la lista: lo que hay que comprar, arriba.
    public IEnumerable<GrupoDeIngredientes> Grupos
    {
        get
        {
            var sinRubro = Lista.Where(x => !x.Libre && x.Rubro is null).ToList();
            if (sinRubro.Count > 0)
            {
                yield return new GrupoDeIngredientes("Sin rubro", sinRubro);
            }

            foreach (var (valor, nombre) in Rubros)
            {
                var deEste = Lista.Where(x => !x.Libre && x.Rubro == valor).ToList();
                if (deEste.Count > 0)
                {
                    yield return new GrupoDeIngredientes(nombre, deEste);
                }
            }

            var libres = Lista.Where(x => x.Libre).ToList();
            if (libres.Count > 0)
            {
                yield return new GrupoDeIngredientes("No se compran", libres);
            }
        }
    }
}

public record GrupoDeIngredientes(string Titulo, IReadOnlyList<FilaIngrediente> Filas);

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

    // para saber en qué grupo va
    public Rubro? Rubro { get; set; }
    public bool Libre { get; set; }
}


// Lo que se edita de un ingrediente: como se llama, en que se mide y como se
// compra. Las cantidades por producto no estan aca a proposito: se cargan en la
// receta de cada producto, que es donde se esta pensando en ese producto.
public class FichaIngrediente
{
    public int IdIngrediente { get; set; }

    public string Nombre { get; set; } = "";

    // Dónde se compra. Nulo en uno que todavía no lo tiene elegido, y en lo
    // que no se compra, que ni lo pide.
    public Rubro? Rubro { get; set; }

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
// lleva el ingrediente, o el de una receta.
//
// Es lo que devuelve RecetaService y de lo que sale, sumado, el número de la
// grilla. No está formateado: la ficha decide cómo se lee cada renglón.
public class ParteDeReceta
{
    public int IdIngrediente { get; set; }

    // el nombre del producto, o el de la receta: «Margarita», «Pomodoro»
    public string Donde { get; set; } = null!;

    // Cuánto lleva. De un producto es por pieza; de una receta es de la RECETA
    // ENTERA, que es como se carga. Por eso viaja el rinde.
    public decimal Cantidad { get; set; }

    public int? Rinde { get; set; }

    // cuántas piezas lo llevan de verdad: las que lo pidieron sin no cuentan
    public int Piezas { get; set; }

    // Cuántas veces hay que hacer la receta para esas piezas, redondeado para
    // arriba. Cero cuando no viene de una receta.
    public int Veces { get; set; }

    public decimal Total { get; set; }

    public bool EsReceta => Rinde is not null;
}

// Un renglón del desglose, ya formateado. La vista no hace cuentas ni decide
// unidades: solo dibuja lo que le llega.
public class RenglonDesglose
{
    public string Donde { get; set; } = null!;

    // «rinde 6», solo en las recetas
    public string? Rinde { get; set; }

    // «4 u», o «1 kg la receta» si es una receta
    public string Cuanto { get; set; } = null!;

    // «× 2 = 8 u»: por cuántas piezas, o por cuántas veces la receta
    public string Sale { get; set; } = null!;
}
