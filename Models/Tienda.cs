using System.ComponentModel;

namespace f4872.Models;

// El estado de la tienda, que es una sola cosa: si toma pedidos o no.
//
// Es una tabla de una fila sola. Podría ser una bandera en el archivo de
// configuración, pero entonces abrirla un martes sería volver a publicar el
// sitio. Vive en la base porque el interruptor está en el panel y lo toca él.
//
// No guarda el texto de cuándo se vuelve: la pantalla de Configuración se
// descartó en el diseño, así que no hay dónde editarlo y es una constante.
public class Tienda
{
    // siempre 1: la restricción de la tabla no deja que haya otra fila
    public int IdTienda { get; set; }

    [DisplayName("Toma pedidos")]
    public bool Abierta { get; set; } = true;

    [DisplayName("Estado")]
    public string Estado => Abierta ? "Abierta" : "Cerrada";
}
