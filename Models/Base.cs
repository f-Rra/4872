using System.ComponentModel;

namespace f4872.Models;

public class Base
{
    public int IdBase { get; set; }

    public string Nombre { get; set; } = null!;

    // se amasa por tanda entera y no de a un bollo: 1 kg de harina da 6 bollos.
    // La receta se carga como el la dice y el calculo divide por el rinde
    [DisplayName("Rinde")]
    public int Rinde { get; set; } = 1;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();

    [DisplayName("Se carga")]
    public string ComoSeCarga => Rinde > 1 ? $"por tanda de {Rinde}" : "de a uno";
}
