namespace f4872.ViewModels;

// Lo que manda el checkout al confirmar.
//
// Los items van tal cual viven en el navegador: clave a cantidad, sin precios y
// sin nombres. Es a propósito. Lo que llega de afuera dice QUÉ se pidió; cuánto
// sale y cómo se llama lo vuelve a leer el servidor de la base, porque esto lo
// escribe una máquina que no es la nuestra y puede decir cualquier cosa.
public class PedidoNuevo
{
    public string? Cliente { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    // "p12", "p12|3,7" o "e5x12", las mismas tres formas que escribe tienda.js
    public Dictionary<string, int>? Items { get; set; }
}
