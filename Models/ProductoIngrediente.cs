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
}
