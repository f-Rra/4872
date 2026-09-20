using System.Globalization;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using f4872.Data;
using f4872.Helpers;
using f4872.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// una sola cultura para toda la app, fijada antes que nada: asi los precios y las
// fechas salen iguales en las vistas, en los formularios y en lo que corra fuera
// de un request. No se usa UseRequestLocalization porque eso es para sitios en
// varios idiomas, y este habla uno solo
CultureInfo.DefaultThreadCurrentCulture = Cultura.Argentina();
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentCulture;

var builder = WebApplication.CreateBuilder(args);

// ---------- lo que pide el servidor donde vive ----------
// En la máquina de uno ninguna de las dos cosas hace falta. En un hosting son
// la diferencia entre que la página abra y que no.

// El puerto lo elige el servicio y lo pasa por variable de entorno. El que no
// escucha ahí no recibe un solo pedido, por más que el proceso esté vivo.
//
// Que exista esa variable quiere decir que hay alguien adelante: el proxy del
// servicio, que atiende en el 443 y desencripta. La app escucha HTTP nomás, así
// que hay que decirle a qué puerto reenviar, porque el suyo no es. Sin esto
// avisa «Failed to determine the https port for redirect» y entrega la página
// sin encriptar en vez de mandar al visitante al candado.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puerto))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");
    builder.Services.AddHttpsRedirection(opciones => opciones.HttpsPort = 443);
}

// Detrás de ese proxy el pedido llega por HTTP aunque el visitante haya entrado
// por HTTPS, y el único que sabe cómo entró de verdad es el proxy, que lo
// cuenta en una cabecera. Sin leerla, la app decide todo como si nadie usara el
// candado: la cookie del panel sale sin la marca de segura, el aviso de HSTS no
// sale nunca —solo se manda sobre HTTPS— y el registro anota la dirección del
// proxy en lugar de la del que entró.
//
// Las redes y los proxys conocidos se vacían porque la dirección del proxy no
// se sabe de antemano: es un contenedor y cambia en cada despliegue.
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    opciones.KnownNetworks.Clear();
    opciones.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();

// El panel lo usa una persona: una clave y una cookie, sin ASP.NET Identity.
// La cookie es persistente y dura un mes, porque es su computadora y tener que
// escribir la clave cada sabado a la manana no protege de nada.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "4872.panel";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.LoginPath = "/panel/entrar";
        opciones.LogoutPath = "/panel/salir";
        opciones.AccessDeniedPath = "/panel/entrar";
        opciones.ReturnUrlParameter = "volverA";
        opciones.ExpireTimeSpan = TimeSpan.FromDays(30);
        opciones.SlidingExpiration = true;
    });

// Los servicios que alquilan una base no dan la cadena en el formato de Npgsql:
// dan una dirección, postgresql://usuario:clave@maquina:puerto/base. Npgsql no
// la entiende y la app no arranca. Se traduce acá para poder pegar la variable
// tal como viene, que es todo lo que hay que hacer el día que la base se mude.
static NpgsqlConnectionStringBuilder ArmarConexion(string? cadena)
{
    if (string.IsNullOrWhiteSpace(cadena) ||
        !(cadena.StartsWith("postgres://") || cadena.StartsWith("postgresql://")))
    {
        return new NpgsqlConnectionStringBuilder(cadena);
    }

    var direccion = new Uri(cadena);
    // en una dirección el usuario y la clave van escapados: una clave con @ o
    // con / llega escrita %40 y %2F, y así hay que devolverla
    var credencial = direccion.UserInfo.Split(':', 2);

    return new NpgsqlConnectionStringBuilder
    {
        Host = direccion.Host,
        Port = direccion.Port > 0 ? direccion.Port : 5432,
        Database = direccion.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(credencial[0]),
        Password = credencial.Length > 1 ? Uri.UnescapeDataString(credencial[1]) : null
    };
}

// la cadena vive en appsettings sin la clave, y la clave viaja aparte por los
// secretos de usuario para que no termine en el repositorio. En produccion la
// cadena llega entera por variable de entorno, ya con la clave adentro
var conexion = ArmarConexion(builder.Configuration.GetConnectionString("Postgres"));
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

// Las llaves con las que se firma la cookie del panel. De fábrica viven en el
// disco del contenedor, que se rehace entero en cada despliegue: publicar una
// vez invalidaba una cookie pensada para durar un mes.
//
// Van a la base y no a un disco aparte. Un volumen también sobrevive, pero
// depende de que el proveedor lo monte con el dueño correcto: Railway lo monta
// como bind mount, que llega de root, y la app —que corre sin privilegios— no
// puede escribir adentro. La base, en cambio, ya está y es la misma en
// cualquier lado, así que esto no necesita que nadie configure nada.
//
// Va acá abajo y no junto a la cookie porque necesita el contexto registrado.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<Contexto>()
    // el nombre va escrito y no sale de la carpeta donde corre la app: si algún
    // día esa carpeta cambia, las llaves guardadas tienen que seguir abriendo
    // las cookies que ya firmaron
    .SetApplicationName("4872");
// El timeout corto es a proposito: el cliente esta esperando la respuesta de su
// pedido y el aviso no puede hacerlo esperar mas que eso.
//
// El socket va forzado a IPv4, y no es un capricho. api.telegram.org resuelve
// primero a una direccion v6; en una red con IPv6 habilitado pero sin ruta que
// funcione, el cliente de .NET se queda esperando en esa y nunca prueba la v4.
// Medido: sin esto no entra ni en 30 segundos, con esto tarda 600 ms.
builder.Services.AddHttpClient(nameof(TelegramService), c => c.Timeout = TimeSpan.FromSeconds(8))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        ConnectCallback = async (contexto, corte) =>
        {
            var enchufe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await enchufe.ConnectAsync(contexto.DnsEndPoint, corte);
                return new NetworkStream(enchufe, ownsSocket: true);
            }
            catch
            {
                enchufe.Dispose();
                throw;
            }
        }
    });
builder.Services.AddScoped<TelegramService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<RecetaService>();

var app = builder.Build();

// Las dos masas van siempre, tambien en produccion: son la receta del vendedor
// y no datos de prueba. Sin ellas, el primer producto que se cargue queda sin
// masa. La carta inventada, en cambio, solo en la maquina de uno.
{
    using var alcance = app.Services.CreateScope();
    var contexto = alcance.ServiceProvider.GetRequiredService<Contexto>();
    var registro = alcance.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Las migraciones corren al arrancar y no a mano. Publicar tiene que ser un
    // push: a la base del servidor no la va a actualizar nadie desde su máquina,
    // y la primera vez no hay ni una tabla. Va antes de sembrar, que escribe
    // justo en las tablas que esto crea.
    await contexto.Database.MigrateAsync();

    await Sembrador.SembrarLasMasas(contexto, registro);

    if (app.Environment.IsDevelopment())
    {
        await Sembrador.SembrarLaCartaDePrueba(contexto, registro);
    }
}

// Antes que nada: todo lo que sigue —el redirector a HTTPS, la cookie del
// panel, cualquier dirección que se arme sola— necesita saber por dónde entró
// el cliente de verdad, y eso viene en las cabeceras que puso el proxy.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tienda}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
