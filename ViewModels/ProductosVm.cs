using f4872.Models;

namespace f4872.ViewModels;

public class ProductosVm : PanelVm
{
    // "todo" o el nombre de una familia, para marcar el chip encendido
    public string Familia { get; set; } = "todo";

    public IReadOnlyList<FilaProducto> Lista { get; set; } = [];

    // «12 · ninguno agotado», al lado del título
    public string Resumen { get; set; } = "";

    // La ficha de la derecha. En un alta viene en blanco: no hay una pantalla
    // de alta distinta de la de edición, es la misma ficha vacía.
    public FichaProducto Ficha { get; set; } = new();

    public bool EsNuevo { get; set; }

    // El encabezado de la ficha sale del producto guardado y no de lo tipeado:
    // es la identidad de lo que estás editando. Si no, al fallar el guardado
    // con el nombre vacío, el título quedaba en blanco.
    public string Titulo { get; set; } = "";
    public string Subtitulo { get; set; } = "";
    public bool ActivoGuardado { get; set; } = true;

    // El lugar en la carta, contado dentro de su familia: «4» y «de 7
    // pizzas». También es de lo guardado, como el título, y no viaja en el
    // formulario: se cambia con las flechas, que guardan solas.
    public int Lugar { get; set; }
    public int Cuantos { get; set; }
    public string DeCuantos { get; set; } = "";

    // lo que salió mal al guardar, con el texto que va a leer una persona
    public string? Error { get; set; }

    public static readonly (string Clave, string Nombre)[] Chips =
    [
        ("todo", "Todo"),
        (nameof(Models.Familia.Pizza), "Pizzas"),
        (nameof(Models.Familia.Focaccia), "Focaccias"),
        (nameof(Models.Familia.Empanada), "Empanadas")
    ];
}

public class FilaProducto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = null!;
    public Familia Familia { get; set; }
    public decimal? Precio { get; set; }
    public bool Activo { get; set; }
    public int Posicion { get; set; }
}

public class FichaProducto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = "";
    public Familia Familia { get; set; } = Familia.Pizza;
    public decimal? Precio { get; set; }
    public bool Activo { get; set; } = true;

    // Los precios de pack no son de este producto: son de todos. Viajan en la
    // ficha porque es donde se editan —la de cualquier gusto— y no porque le
    // pertenezcan al gusto que se esté mirando.
    //
    // List y no IReadOnlyList: esta es la única lista que vuelve por POST, y el
    // binder necesita poder agregarle elementos para armarla.
    public List<PrecioPack> Packs { get; set; } = [];

    // List y no IReadOnlyList por lo mismo que los packs: las cantidades vuelven
    // por el formulario de la ficha y el binder necesita poder armarla
    public List<IngredienteDeLaReceta> Receta { get; set; } = [];

    // Los que todavía no están en la receta, con su unidad. Solo se puede sumar
    // uno que ya exista: escribir libre es como entraron «Oregano» y «Orégano»
    // a la base de la maqueta como dos ingredientes distintos.
    public IReadOnlyList<IngredienteDisponible> Disponibles { get; set; } = [];

    // El ingrediente ya elegido que está esperando la cantidad. Es el estado del
    // renglón de arriba: nulo en reposo, con nombre mientras se carga.
    public IngredienteDeLaReceta? Sumando { get; set; }

    // El bollo que amasa este producto. Nulo en las empanadas, que no amasan
    // nada: la tapa se compra hecha y es un ingrediente más.
    //
    // Vive en la ficha por lo mismo que los precios de pack: es donde se lo
    // mira. Y como ellos, no es de este producto —cambiarlo lo cambia para
    // todos los que lo amasan— así que se cambia en Recetas.
    public RecetaPorPieza? Base { get; set; }

    // La salsa elegida, o nula si no lleva. A diferencia de la base se elige,
    // y por eso viaja con la ficha como el precio.
    public int? IdSalsa { get; set; }

    // Todas las salsas, para elegir, cada una con lo que le toca a una pizza:
    // la elegida se ve abierta, y al tocar otra se abre la otra sin esperar a
    // guardar.
    public IReadOnlyList<RecetaPorPieza> Salsas { get; set; } = [];

    // El relleno de una empanada, y todos para elegir, igual que la salsa. Es
    // todo lo que lleva la empanada: no tiene ingredientes sueltos.
    public int? IdRelleno { get; set; }
    public IReadOnlyList<RecetaPorPieza> Rellenos { get; set; } = [];

    public bool SeCobraPorPack => Familia == Familia.Empanada;

    public string NombreDeFamilia => Familia switch
    {
        Familia.Pizza => "Pizza",
        Familia.Focaccia => "Focaccia",
        _ => "Empanada"
    };
}

// Un renglón de la receta: qué lleva, cuánto y si el cliente lo puede sacar.
//
// Los tres juntos acá y no repartidos entre dos pantallas: cuando estás cargando
// una pizza la pregunta es «qué lleva y qué se le puede sacar», y tener que ir a
// Ingredientes por el cuánto obliga a ir y volver por cada renglón.
public class IngredienteDeLaReceta
{
    public int IdIngrediente { get; set; }
    public string Nombre { get; set; } = "";
    public decimal Cantidad { get; set; }

    // «g», «ml» o «u». Es del ingrediente, no del renglón: no se edita acá
    public string Unidad { get; set; } = "";

    // si el cliente puede pedir la pizza sin esto. Es del par producto-
    // ingrediente: la muzzarella se saca de una fugazzeta y de una napolitana no
    public bool Modificable { get; set; }
}

// la unidad viaja con el nombre para poder mostrarla al lado del campo de
// cuánto apenas se elige, sin otro viaje al servidor
public class IngredienteDisponible
{
    public string Nombre { get; set; } = null!;
    public string Unidad { get; set; } = null!;
}

public class PrecioPack
{
    public int Unidades { get; set; }
    public decimal Precio { get; set; }
}

// Lo que le toca a UNA pieza de una receta que usa el producto: la masa o la
// salsa. Una pizza es un bollo.
//
// Las recetas se cargan enteras, que es como se hacen, pero acá no se muestran
// así: en esta ficha todo lo demás es por pieza —120 g de muzzarella son de una
// pizza— y mezclar las dos cuentas en la misma pantalla obligaba a explicar el
// rinde. Divididas, las listas dicen lo mismo.
//
// Va de lectura: son números derivados, y la receta no es de este producto sino
// de todos los que la usan.
public class RecetaPorPieza
{
    public int IdReceta { get; set; }
    public string Nombre { get; set; } = null!;
    public IReadOnlyList<RenglonPorPieza> Renglones { get; set; } = [];
}

// Un renglón de la receta, ya formateado: «166,7 g».
public class RenglonPorPieza
{
    public string Nombre { get; set; } = null!;
    public string Cuanto { get; set; } = null!;
}
