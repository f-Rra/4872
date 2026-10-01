using System.ComponentModel;

namespace f4872.Models;

public class ProductoIngrediente
{
    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    public int IdIngrediente { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    // la cantidad es del par y no del ingrediente: la fugazzeta lleva 200 g de
    // cebolla y la empanada de carne 15. El mismo ingrediente pesa distinto en
    // cada producto, asi que el numero no puede vivir en el ingrediente
    [DisplayName("Cantidad")]
    public decimal Cantidad { get; set; }

    // el quitable va en el mismo par y por el mismo motivo: la muzzarella se
    // saca de una fugazzeta, y de una napolitana no
    [DisplayName("Se puede sacar")]
    public bool Quitable { get; set; }

    // El lugar en la receta, que es el orden en que la carta lo nombra. Es del
    // par por lo mismo que la cantidad: la muzzarella va primera en una y
    // última en otra. Sirve solo para ordenar, así que puede tener huecos o
    // repetirse: desempata el IdIngrediente.
    public int Posicion { get; set; }

    // Si la carta lo nombra. Una receta lleva lo que hace falta para el costo,
    // y eso es más de lo que entra en una línea: lo que no se ve sigue contando
    // en la compra y la producción, pero el cliente no sabe que está y por eso
    // no lo puede sacar. Como mucho Producto.MaximoEnLaCarta por producto,
    // contando las salsas.
    public bool Visible { get; set; } = true;
}
