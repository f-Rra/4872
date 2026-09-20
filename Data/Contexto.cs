using f4872.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace f4872.Data;

// IDataProtectionKeyContext es lo unico que pide ASP.NET para guardar acá las
// llaves con las que firma la cookie del panel. Ver Program.cs
public class Contexto : DbContext, IDataProtectionKeyContext
{
    public Contexto(DbContextOptions<Contexto> opciones) : base(opciones) { }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Ingrediente> Ingredientes => Set<Ingrediente>();
    public DbSet<Base> Bases => Set<Base>();
    public DbSet<ProductoIngrediente> ProductoIngredientes => Set<ProductoIngrediente>();
    public DbSet<BaseIngrediente> BaseIngredientes => Set<BaseIngrediente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItemPedidos => Set<ItemPedido>();
    public DbSet<ItemQuitado> ItemQuitados => Set<ItemQuitado>();
    public DbSet<Pack> Packs => Set<Pack>();
    public DbSet<Tienda> Tienda => Set<Tienda>();

    // No es una entidad del negocio: la tabla y su forma las define ASP.NET, y
    // el nombre de la propiedad tampoco se elige, lo busca por la interfaz.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // levanta sola cada XConfiguracion de Data/Configuraciones, asi sumar una
        // entidad no obliga a volver a tocar este archivo
        modelo.ApplyConfigurationsFromAssembly(typeof(Contexto).Assembly);
    }
}
