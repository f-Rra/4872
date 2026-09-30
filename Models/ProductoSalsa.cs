namespace f4872.Models;

// Una salsa que lleva un producto. Es un par, como ProductoIngrediente, y por
// lo mismo: una pizza puede llevar dos, y en qué lugar la nombra la carta es
// de esa salsa en esa pizza, no de la salsa.
public class ProductoSalsa
{
    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    // una receta de tipo Salsa
    public int IdReceta { get; set; }
    public Receta Receta { get; set; } = null!;

    // El lugar en la lista del producto. Se cuenta junto con el de los
    // ingredientes, porque la carta los nombra a todos en un solo renglón. Como
    // allá, sirve solo para ordenar.
    public int Posicion { get; set; }

    // Cuántas porciones de la receta lleva esta pizza. Una porción es la receta
    // dividida por su rinde -la salsa que rinde seis pizzas, un sexto-, y la
    // cuenta por pieza se hace igual que con la base. Va del par y no de la
    // salsa por lo mismo que la cantidad de un ingrediente: una pizza puede
    // llevar media y otra dos.
    public decimal Porciones { get; set; } = 1;

    // Si el cliente puede pedir la pizza sin esta salsa. Del par, como el de un
    // ingrediente: el pesto se saca de una y de otra no.
    public bool Quitable { get; set; }
}
