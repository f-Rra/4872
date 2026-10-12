namespace f4872.Models;

// las empanadas no tienen precio propio: se cobran por pack de 6 o de 12, de un
// solo gusto. El precio vive acá porque no es de ningún producto en particular
public class Pack
{
    public int IdPack { get; set; }

    public int Unidades { get; set; }

    public decimal Precio { get; set; }

    public bool Activo { get; set; } = true;
}
