# La imagen se arma en dos etapas. La primera trae el SDK entero para compilar;
# la segunda, solo lo necesario para correr. Lo que viaja al servidor es la
# segunda: el SDK pesa mas de un giga y alla no se compila nada.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS construir
WORKDIR /obra

# El .csproj primero y solo el .csproj. Bajar los paquetes es el paso lento, y
# asi esa capa se rehace unicamente cuando cambian las dependencias: tocar una
# vista no vuelve a bajar EF Core ni Npgsql.
COPY f4872.csproj ./
RUN dotnet restore f4872.csproj

COPY . ./
# El proyecto y no la solucion: con la solucion, -o avisa que todos los
# proyectos van a escribir en la misma carpeta
RUN dotnet publish f4872.csproj -c Release -o /publicado


# aspnet y no runtime, que no trae ASP.NET Core. Esta es la de Debian, que
# incluye ICU, y de ahi salen los precios en es-AR: "$9.800", con punto.
# Cambiarla por la de Alpine sin agregarle icu-libs, o prender
# InvariantGlobalization, los escribe "$9,800" y no avisa nada.
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=construir /publicado ./

# Escrito aunque ya sea el valor por omision, porque es el error que no se puede
# permitir: en Development el sembrador carga la carta inventada, y esa carta no
# tiene que tocar la base de verdad ni una vez. Ver Data/Sembrador.cs
ENV ASPNETCORE_ENVIRONMENT=Production

# La carpeta donde se montan las llaves de la cookie, creada aca y con dueno.
# Un volumen nuevo hereda el dueno de la carpeta que encuentra en la imagen; si
# no encuentra ninguna, la crea de root y el usuario de abajo no puede escribir
# adentro. Medido: sin esto la app no arranca, "Access to the path '/datos'
# is denied". Ver Llaves:Carpeta en Program.cs
RUN mkdir -p /datos && chown $APP_UID /datos

# La imagen trae un usuario sin privilegios. Si algun dia entran por un agujero
# de la app, entran como el y no como root.
USER $APP_UID

# Documentacion nomas: el puerto de verdad lo manda el servicio en PORT y lo lee
# Program.cs. Sin esa variable, la imagen escucha en el 8080.
EXPOSE 8080

ENTRYPOINT ["dotnet", "f4872.dll"]
