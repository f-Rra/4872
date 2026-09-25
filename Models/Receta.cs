using System.ComponentModel;

namespace f4872.Models;

// Lo que se prepara aparte y usan varios productos: la masa, la salsa o el
// relleno. No es lo que lleva cada producto —eso es Producto.Receta— sino algo
// que se hace antes, para muchos a la vez.
public class Receta
{
    public int IdReceta { get; set; }

    public string Nombre { get; set; } = null!;

    public TipoReceta Tipo { get; set; }

    // Qué familia la amasa, y solo en las bases: una salsa o un relleno no se
    // amasan. Va acá y no deducido de los productos que ya la usan: en una base
    // recién creada no hay ninguno, y el primer producto que se cargara quedaba
    // sin masa. Las empanadas no amasan, así que no tienen base propia.
    [DisplayName("La amasa")]
    public Familia? Familia { get; set; }

    // Se hace entera y no de a una pieza: 1 kg de harina da 6 bollos. Se carga
    // como él la dice y el cálculo divide por el rinde
    [DisplayName("Rinde")]
    public int Rinde { get; set; } = 1;

    // las pizzas o las focaccias que la amasan: solo una base tiene productos
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();

    public ICollection<RecetaIngrediente> Ingredientes { get; set; } = new List<RecetaIngrediente>();
}
