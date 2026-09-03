using f4872.Models;
using Microsoft.EntityFrameworkCore;

namespace f4872.Data;

public class Contexto : DbContext
{
    public Contexto(DbContextOptions<Contexto> opciones) : base(opciones) { }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Ingrediente> Ingredientes => Set<Ingrediente>();
    public DbSet<Base> Bases => Set<Base>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // levanta sola cada XConfiguracion de Data/Configuraciones, asi sumar una
        // entidad no obliga a volver a tocar este archivo
        modelo.ApplyConfigurationsFromAssembly(typeof(Contexto).Assembly);
    }
}
