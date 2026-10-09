# ---------- Compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /src
COPY Ferreteria/Ferreteria.csproj Ferreteria/
RUN dotnet restore Ferreteria/Ferreteria.csproj
COPY Ferreteria/ Ferreteria/
RUN dotnet publish Ferreteria/Ferreteria.csproj -c Release -o /app --no-restore

# ---------- Ejecución ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilacion /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Usuario sin privilegios (incluido en la imagen oficial); solo puede escribir en las imágenes subidas
# y en la carpeta de llaves de cifrado (montada como volumen para que sobreviva a los reinicios)
RUN mkdir -p /app/wwwroot/uploads /llaves && chown app /app/wwwroot/uploads /llaves
ENV Seguridad__DirectorioLlaves=/llaves
USER app
ENTRYPOINT ["dotnet", "Ferreteria.dll"]
