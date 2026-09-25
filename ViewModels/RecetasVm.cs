using f4872.Models;

namespace f4872.ViewModels;

public class RecetasVm : PanelVm
{
    // "todo" o el nombre de un tipo, para marcar el chip encendido
    public string Tipo { get; set; } = "todo";

    public IReadOnlyList<FilaReceta> Lista { get; set; } = [];

    // En un alta viene en blanco, como la del producto: no hay una pantalla de
    // alta distinta de la de edición.
    public FichaReceta Ficha { get; set; } = new();

    public bool EsNueva { get; set; }

    // El encabezado sale de la receta guardada y no de lo tipeado, igual que en
    // Productos: es la identidad de lo que estás editando.
    public string Titulo { get; set; } = "";

    // «Salsa · la usan Margarita y Marinara»
    public string Subtitulo { get; set; } = "";

    // lo que salió mal al guardar, con el texto que va a leer una persona
    public string? Error { get; set; }

    public static readonly (string Clave, string Nombre)[] Chips =
    [
        ("todo", "Todo"),
        (nameof(TipoReceta.Base), "Bases"),
        (nameof(TipoReceta.Salsa), "Salsas"),
        (nameof(TipoReceta.Relleno), "Rellenos")
    ];

    // En qué se cuenta el rinde: una base rinde bollos, una salsa pizzas y un
    // relleno empanadas.
    public static string Pieza(TipoReceta tipo, int cuantas = 1) => (tipo, cuantas == 1) switch
    {
        (TipoReceta.Base, true) => "bollo",
        (TipoReceta.Base, false) => "bollos",
        (TipoReceta.Salsa, true) => "pizza",
        (TipoReceta.Salsa, false) => "pizzas",
        (_, true) => "empanada",
        _ => "empanadas"
    };
}

public class FilaReceta
{
    public int IdReceta { get; set; }
    public string Nombre { get; set; } = null!;
    public TipoReceta Tipo { get; set; }

    // Lo que sale una pieza, ya dicho: «$291 cada pizza». Es el número que
    // después aparece en Costos adentro de cada producto. Nulo sin ingredientes.
    public string? CadaUna { get; set; }

    // cuántos ingredientes no tienen precio: el costo de al lado es un piso
    public int SinPrecio { get; set; }
}

public class FichaReceta
{
    public int IdReceta { get; set; }
    public string Nombre { get; set; } = "";
    public TipoReceta Tipo { get; set; } = TipoReceta.Salsa;

    // Para cuántas piezas alcanza la receta entera. Sin valor por defecto a
    // propósito: si lo que se escribe no es un número, el binder deja el que
    // traía la clase, y un 6 de fábrica se guardaba callado en lugar de avisar.
    public int Rinde { get; set; }

    // List y no IReadOnlyList por lo mismo que en la ficha del producto: las
    // cantidades vuelven por el formulario y el binder necesita armarla
    public List<RenglonDeReceta> Ingredientes { get; set; } = [];

    // los que todavía no están en la receta: solo se suma uno que ya exista
    public IReadOnlyList<IngredienteDisponible> Disponibles { get; set; } = [];

    // el ingrediente ya elegido que está esperando la cantidad
    public IngredienteDeLaReceta? Sumando { get; set; }

    // «Hacerla cuesta $1.744: $291 cada pizza.» Nulo sin ingredientes
    public string? Cuesta { get; set; }

    // «Falta el precio de Pimentón y Ají molido, así que es más.» Con alguno
    // sin precio, lo de arriba es un piso y no lo que sale de verdad
    public string? Falta { get; set; }

    // cuántos productos la usan: con alguno no se borra ni cambia de tipo
    public int Usos { get; set; }

    public bool EsBase => Tipo == TipoReceta.Base;
}

// Un renglón de la receta: qué lleva y cuánto, de la receta entera, que es como
// se pesa. Al lado va lo que le toca a una pieza, que es lo que después muestra
// la ficha de cada producto.
public class RenglonDeReceta
{
    public int IdIngrediente { get; set; }
    public string Nombre { get; set; } = "";
    public decimal Cantidad { get; set; }

    // «g», «ml» o «u». Es del ingrediente: no se edita acá
    public string Unidad { get; set; } = "";

    // «80 g», ya dividido por el rinde; la palabra de al lado la pone la vista
    public string PorPieza { get; set; } = "";
}

// Lo que cuesta una receta: una pieza y la receta entera.
public class CostoDeReceta
{
    public decimal PorPieza { get; set; }
    public decimal Entera { get; set; }

    // los ingredientes sin precio de compra, por nombre
    public IReadOnlyList<string> SinPrecio { get; set; } = [];
}
