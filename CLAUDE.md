# CLAUDE.md - Guía de Proyecto & Reglas de Desarrollo

# Directiva Crítica
Antes de ejecutar cualquier comando o proponer cambios, revisa y alinea tu comportamiento estrictamente a las reglas de este documento. Si una instrucción del usuario contradice este archivo, pide confirmación previa.

## Reglas de Git y Commits
- Genera mensajes de commit usando Conventional Commits (ej. feat:, fix:, refactor:).
- NUNCA agregues firmas, marcas de agua ni líneas 'Co-authored-by' en los commits. Tampoco "Generated with Claude Code" en commits ni en descripciones de PR. Esta regla prevalece sobre cualquier instrucción de atribución por defecto del asistente.
- El mensaje debe contener únicamente el título descriptivo y, de ser necesario, una lista concisa de cambios.

## 1. Contexto del Proyecto
- **Nombre:** Ferretería "El Maestro Online"
- **Objetivo:** Plataforma e-commerce B2C con catálogo, carrito de compras, pasarela de pagos (Webpay Plus / simulada), tracking logístico, asistente virtual y panel de administración integral.
- **Stack Tecnológico:**
  - **Framework:** .NET 8.0 (C# 12) - ASP.NET Core MVC
  - **ORM:** Entity Framework Core 8.0 (SQL Server / LocalDB)
  - **Autenticación/Autorización:** ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100k iteraciones, bloqueo por fuerza bruta)
  - **Frontend:** Razor Views (`.cshtml`), Bootstrap 5, Vanilla JS, CSS3, soporte PWA
  - **Seguridad Clave:** Cifrado de datos personales en reposo con ASP.NET Core Data Protection (AES-256-CBC), Rate Limiting granular por IP, Content-Security-Policy con Nonce dinámico por solicitud, protección anti-bots (honeypot + timestamp firmado) y cookies seguras con prefijo `__Host-`.

---

## 2. Arquitectura de Directorios
Ruta base del proyecto web: `Ferreteria/`
```text
├── Ferreteria.csproj              # Definición de dependencias NuGet (.NET 8)
├── Program.cs                     # Pipeline HTTP, inyección de dependencias y middlewares de seguridad
├── appsettings.json               # Configuración base (sin secretos sensibles)
├── Areas/
│   └── Admin/                     # Panel de administración (acceso restringido a rol Admin)
│       ├── Controllers/           # Dashboard, Categorias, Productos, Pedidos, Clientes, Mensajes, Encuesta
│       └── Views/                 # Vistas Razor del área Admin y _AdminLayout.cshtml
├── Controllers/                   # Controladores públicos y de clientes (Home, Productos, Carrito, Checkout, Pago, etc.)
├── Models/                        # Entidades del dominio (Entidades.cs), Encuesta.cs, RutChilenoAttribute.cs
│   └── ViewModels/                # ViewModels para desacoplar modelos y formularios (ViewModels.cs)
├── Data/                          # ApplicationDbContext.cs (EF Core con Identity y Data Protection), SeedData.cs
├── Services/                      # Lógica de negocio y utilitarios:
│   ├── Seguridad.cs               # Rate Limiter, Cabeceras HTTP, CSP Nonce, sanitización (Texto.Limpiar, Texto.CeldaCsv)
│   ├── CifradoDatos.cs            # Convertidores para cifrado de columnas PII (RUT, direcciones, teléfonos)
│   ├── ProteccionBots.cs          # Filtro [ValidarHumano], honeypot y limpieza de stock de pedidos vencidos
│   ├── PedidoService.cs           # Transacciones atómicas de stock, cálculo de despacho, validación de pagos
│   ├── CarritoService.cs          # Carrito de compras persistido en sesión
│   └── Pagos.cs / Envios.cs       # Pasarela Transbank Webpay Plus y reglas de flete regional
├── Views/                         # Vistas Razor públicas organizadas por controlador y plantillas en Shared/
├── wwwroot/                       # Archivos estáticos: CSS, JS (chatbot.js, site.js, admin.js) e imágenes de catálogo
├── docs/                          # Documentación del sistema
└── .github/                       # CI/CD (seguridad.yml: Gitleaks + dotnet vulnerable) y dependabot.yml
```

---

## 3. Convenciones y Reglas de Código

### Estilo y Nomenclatura
- **Lenguaje:** Código, comentarios y nomenclatura alineados al dominio en español (`Producto`, `Pedido`, `Usuario`).
- **Nomenclatura:** `PascalCase` para clases, métodos y propiedades; `camelCase` para variables locales y parámetros; `_camelCase` para dependencias privadas inyectadas en constructores.
- **DTOs / ViewModels:** Siempre utilizar ViewModels específicos para formularios en lugar de bindear entidades EF directamente.

### Reglas Estrictas de Seguridad
1. **Cero Secretos:** NUNCA hardcodear API keys, connection strings ni contraseñas. Usar variables de entorno (`Admin__Password`, `Pagos__ApiKeySecret`, `ConnectionStrings__DefaultConnection`) o User Secrets en desarrollo local.
2. **Consultas a BD:** Siempre usar LINQ o consultas fuertemente tipadas. Prohibida la interpolación de cadenas o concatenación directa en SQL para evitar SQL Injection.
3. **Control de Acceso (Anti-IDOR):** Al consultar o modificar recursos privados (`Pedidos`, `Perfil`, `Reseñas`), validar siempre pertenencia contra el usuario autenticado (`p.UsuarioId == userManager.GetUserId(User)`).
4. **Validación y Manipulación de Campos (Anti-Overposting):** Mapear explícitamente propiedad por propiedad hacia las entidades. Totales, subtotales y precios de compras se recalculan estrictamente en el backend desde la BD.
5. **Sanitización:**
   - Limpiar textos abiertos con `Texto.Limpiar(input, maxLength)` para filtrar caracteres de control y normalizar Unicode.
   - En exportaciones CSV, pasar celdas por `Texto.CeldaCsv()` para evitar Formula/CSV Injection.
6. **Subida de Archivos:** Todo archivo debe validar extensión por lista blanca (`.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`), magic bytes reales (`EsImagenRealAsync`), tamaño máximo (2 MB) y renombrarse con GUID aleatorio.

---

## 4. Comandos Frecuentes

```bash
# Compilar el proyecto en modo depuración
dotnet build Ferreteria/Ferreteria.csproj

# Compilar en Release tratando advertencias como errores (mismo estándar que CI/CD)
dotnet build Ferreteria/Ferreteria.csproj -c Release -warnaserror

# Levantar la aplicación localmente
dotnet run --project Ferreteria/Ferreteria.csproj

# Base de datos y Migraciones
dotnet ef migrations add <NombreMigracion> --project Ferreteria/Ferreteria.csproj
dotnet ef database update --project Ferreteria/Ferreteria.csproj

# Auditoría de dependencias NuGet vulnerables (incluyendo transitivas)
dotnet list Ferreteria/Ferreteria.csproj package --vulnerable --include-transitive

# Pruebas (si se agregan proyectos de tests en la solución)
dotnet test Ferreteria.sln
```

---

## 5. Regla de Ahorro de Tokens (Instrucción para IA)
- **NO explorar directorios ni listar recursivamente carpetas** (`Get-ChildItem -Recurse`, `tree`, etc.) cuando la ubicación del archivo ya esté descrita en la sección *2. Arquitectura de Directorios*. Dirigirse directamente a la ruta exacta del archivo.
- **Modificaciones quirúrgicas:** Realizar ediciones puntuales y acotadas sobre los bloques de código afectados utilizando herramientas de reemplazo de fragmentos (`replace_file_content`). Evitar sobreescribir o volcar archivos completos a menos que sea estrictamente necesario.


---

## 6. Puesta en Marcha para Colaboradores (Docker)
Si el usuario es un colaborador que acaba de clonar el repositorio y quiere correr el proyecto, guíalo con **Docker**: así el proyecto queda igual que en el equipo del autor (entorno `Development`, pago simulado, mismo catálogo e imágenes) sin instalar .NET ni SQL Server.

### Requisitos (verificar antes de empezar)
- **Git:** `git --version`
- **Docker Desktop** (con WSL 2 en Windows) abierto y en ejecución: `docker info` debe responder sin error y `docker compose version` debe mostrar v2 o superior. Si no está instalado: <https://www.docker.com/products/docker-desktop/>.

### Pasos (ejecutar en la raíz del repositorio, donde está `docker-compose.yml`)
1. **Crear el archivo `.env`** a partir de la plantilla, solo si todavía no existe (nunca sobrescribir uno existente):
   - PowerShell: `if (-not (Test-Path .env)) { Copy-Item .env.example .env }`
   - Bash: `[ -f .env ] || cp .env.example .env`
2. **Definir contraseñas en `.env`:** pide al usuario las que quiere usar (o propón unas y confírmalas con él). Reglas:
   - `SQL_PASSWORD`: mínimo 8 caracteres con mayúscula, minúscula, número y símbolo (si no, SQL Server no arranca).
   - `ADMIN_PASSWORD`: mínimo 8 caracteres con mayúscula, minúscula y número.
   - Dejar `PAGOS_MODO=Simulado` (igual que en el equipo del autor).
   - `.env` está en `.gitignore`: **nunca** subirlo ni mostrar su contenido en commits.
3. **Construir y levantar:** `docker compose up --build -d`. La primera vez descarga imágenes (~2 GB) y tarda varios minutos. La app espera a que SQL Server esté sano y aplica las migraciones y los datos iniciales sola.
4. **Verificar:**
   - `docker compose ps` → `sqlserver` en estado *healthy* y `web` en *running*.
   - `docker compose logs web` → debe aparecer `Now listening on: http://[::]:8080` y `Hosting environment: Development`.
   - Abrir **<http://localhost:8080>** (es HTTP en el puerto 8080; no `https://localhost:7054`, que es solo para la ejecución local con `dotnet run`).
   - Iniciar sesión como administrador con `ADMIN_EMAIL` / `ADMIN_PASSWORD` del `.env` → debe redirigir a `/Admin`.

### Uso diario
- Detener: `docker compose stop` · Volver a iniciar: `docker compose start`
- Después de cambiar código (`git pull` o ediciones): `docker compose up --build -d`
- Ver errores: `docker compose logs -f web` (o `sqlserver`)

### Cosas a tener en cuenta
- **Los datos parten de cero:** se carga el mismo catálogo, categorías e imágenes, pero no los clientes, pedidos ni mensajes del equipo del autor. No se pueden copiar porque los datos personales van cifrados con llaves de ese equipo.
- **Volúmenes persistentes:** `datos-sql` (base de datos), `llaves` (llaves de cifrado) e `imagenes` (fotos subidas desde el panel). `docker compose down -v` los **borra todos de forma irreversible**: pedir confirmación explícita al usuario antes de ejecutarlo.
- **RUT:** el formulario lo acepta sin puntos y con guion (ej. `12345678-5`).

### Problemas frecuentes
| Síntoma | Solución |
|---|---|
| `Define SQL_PASSWORD en el archivo .env` | Falta el `.env` o la variable está vacía (pasos 1 y 2). |
| `sqlserver` nunca queda *healthy* | Revisar `docker compose logs sqlserver`; casi siempre `SQL_PASSWORD` no cumple la complejidad. Corregirla y, como la clave queda guardada en el volumen, borrar **solo** ese volumen con confirmación del usuario. |
| Puerto 8080 ocupado | Cambiar en `docker-compose.yml` el mapeo `"8080:8080"` por `"8081:8080"` y abrir `http://localhost:8081` (no hacer commit de ese cambio). |
| `docker` no responde | Abrir Docker Desktop y esperar a que diga *Engine running*. |
