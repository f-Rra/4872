using f4872.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// la cadena vive en appsettings sin la clave, y la clave viaja aparte por los
// secretos de usuario para que no termine en el repositorio. En produccion la
// cadena va a llegar entera por variable de entorno, ya con la clave adentro
var conexion = new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Postgres"));
var clave = builder.Configuration["Postgres:Clave"];
if (!string.IsNullOrWhiteSpace(clave))
{
    conexion.Password = clave;
}

if (string.IsNullOrWhiteSpace(conexion.Password))
{
    // los secretos de usuario solo se cargan en Development, asi que fuera de ahi
    // mandar a user-secrets seria mandar a un lugar que no se lee
    throw new InvalidOperationException(builder.Environment.IsDevelopment()
        ? "Falta la clave de Postgres. Corré esto en la carpeta del proyecto: " +
          "dotnet user-secrets set \"Postgres:Clave\" \"la-clave-que-pusiste-al-instalar-postgres\""
        : "Falta la cadena de conexión a Postgres. Definí la variable de entorno " +
          "ConnectionStrings__Postgres con la cadena completa, clave incluida.");
}

builder.Services.AddDbContext<Contexto>(opciones => opciones.UseNpgsql(conexion.ConnectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
