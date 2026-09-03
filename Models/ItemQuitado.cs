using System.ComponentModel;

namespace f4872.Models;

public class ItemQuitado
{
    public int IdItemPedido { get; set; }
    public ItemPedido Item { get; set; } = null!;

    // el NOMBRE copiado y no una clave al ingrediente, por dos motivos: el
    // pedido tiene que leerse aunque ese ingrediente ya no este en la carta,
    // y un ingrediente que se dejo de usar no puede quedar imposible de
    // borrar por un pedido de hace dos anos
    [DisplayName("Ingrediente")]
    public string Ingrediente { get; set; } = null!;
}
