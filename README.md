# El Maestro Online · E-commerce de ferretería

Tienda online de una ferretería chilena **simulada**, desarrollada como proyecto académico para la asignatura *Adopción de Tecnologías* (TIEL25) de INACAP. Tiene catálogo con ofertas, carrito, compra con o sin cuenta, pago con **Webpay Plus** de Transbank, chatbot, recomendaciones personalizadas, logística por región y panel de administración.

![Página principal](docs/capturas/inicio.jpg)

**Tecnologías:** ASP.NET Core 8 MVC · Entity Framework Core 8 · SQL Server · ASP.NET Core Identity · Bootstrap 5 · Webpay Plus (API REST) · Docker

> ⚠️ Proyecto académico: la tienda, sus datos de contacto y las reseñas de demostración son ficticios. Los pagos usan el ambiente de pruebas de Transbank o un simulador, nunca dinero real.

---

## Índice

1. [Funcionalidades](#1-funcionalidades)
2. [Requisitos](#2-requisitos)
3. [Puesta en marcha paso a paso](#3-puesta-en-marcha-paso-a-paso)
4. [Accesos de prueba](#4-accesos-de-prueba)
5. [Problemas comunes](#5-problemas-comunes)
6. [Alternativa: Docker](#6-alternativa-docker-windows-mac-o-linux)
7. [Configuración y secretos](#7-configuración-y-secretos)
8. [Probar el pago](#8-probar-el-pago)
9. [Cómo funciona por dentro](#9-cómo-funciona-por-dentro)
10. [Seguridad](#10-seguridad)
11. [Estructura del proyecto](#11-estructura-del-proyecto)
12. [Publicar en producción](#12-publicar-en-producción)
13. [Créditos](#13-créditos)

---

## 1. Funcionalidades

| Área | Qué incluye |
|---|---|
| **Página principal** | Slider *Hot Sale* con cuenta regresiva, categorías con foto, ofertas relámpago, "Recomendado para ti", destacados, novedades y animaciones al hacer scroll. |
| **Catálogo** | Búsqueda, filtros por categoría y "solo ofertas", orden por precio, nombre o descuento, ficha con zoom, stock real y reseñas. |
| **Carrito y pago** | Validación de stock, barra hacia el despacho gratis, complementos sugeridos, **compra como invitado** (sin registro) o con cuenta, y pago con Webpay. |
| **Logística** | Tarifas y plazos para las 16 regiones de Chile, despacho gratis sobre $50.000, retiro en tienda, fecha estimada en días hábiles, código de seguimiento y página pública "¿Dónde está mi pedido?". |
| **Cuentas** | Registro con consentimiento informado, inicio de sesión, perfil, historial de pedidos. |
| **Atención al cliente** | Chatbot *Maestro Bot* (preguntas frecuentes y búsqueda de productos), formulario de contacto, WhatsApp y correo en un botón flotante. |
| **Recomendaciones** | Según los productos vistos, las compras anteriores y lo que otros clientes compraron junto. Cada sugerencia explica su motivo. |
| **Panel admin** (`/Admin`) | Resumen de ventas, inventario (productos, ofertas, stock, imágenes), categorías, pedidos por estado con despacho y seguimiento, clientes, mensajes de contacto y resultados de la encuesta. |
| **Encuesta UTAUT** (`/Encuesta`) | Instrumento de aceptación tecnológica de 15 ítems. El admin ve promedios, gráficos, alfa de Cronbach y correlaciones, y puede **descargar las respuestas en Excel (CSV)**. |
| **Legal** | Términos y condiciones, política de privacidad y cookies, cambios y devoluciones, créditos de imágenes (Ley 19.496, Ley 19.628 / 21.719, Ley 17.336). |
| **SEO** | Títulos y descripciones por página, Open Graph, datos estructurados (schema.org), `robots.txt` y `sitemap.xml`. |
| **Móvil** | Diseño adaptable a cualquier pantalla. |

<p align="center">
  <img src="docs/capturas/producto.jpg" width="49%" alt="Ficha de producto con reseñas" />
  <img src="docs/capturas/checkout.jpg" width="49%" alt="Checkout como invitado" />
</p>
<p align="center">
  <img src="docs/capturas/admin-inventario.jpg" width="62%" alt="Panel de administración" />
  <img src="docs/capturas/chatbot.jpg" width="36%" alt="Chatbot Maestro Bot" />
</p>

---

## 2. Requisitos

Para probarlo en Windows (la forma recomendada y probada) necesitas instalar:

| Programa | Para qué | Dónde se obtiene | Cómo comprobar que está |
|---|---|---|---|
| **Git** | Descargar el proyecto | <https://git-scm.com/download/win> | `git --version` |
| **.NET 8 SDK** | Compilar y ejecutar la tienda | <https://dotnet.microsoft.com/download/dotnet/8.0> (elige **SDK 8.0.x**, no solo "Runtime") | `dotnet --list-sdks` → debe aparecer una línea `8.0.xxx` |
| **SQL Server LocalDB** | Base de datos local | Viene con **Visual Studio 2022** (carga de trabajo *"Desarrollo de ASP.NET y web"*) o con el [instalador de SQL Server Express](https://www.microsoft.com/es-es/sql-server/sql-server-downloads) → *Descargar medios* → **LocalDB** | `sqllocaldb info` → debe aparecer `MSSQLLocalDB` |
| Editor *(opcional)* | Ver y editar el código | Visual Studio 2022 o VS Code con la extensión *C# Dev Kit* | — |

**No hay que instalar nada más.** Los paquetes NuGet (Entity Framework Core 8.0.31 y ASP.NET Core Identity 8.0.31) se descargan solos al compilar, y Bootstrap, jQuery y el resto de las librerías del navegador ya vienen dentro del proyecto (`wwwroot/lib`). La tienda funciona sin internet, salvo el modo Webpay.

> En Mac o Linux no existe LocalDB: usa la [alternativa con Docker](#6-alternativa-docker-windows-mac-o-linux) o un SQL Server en Docker (al final de la sección 3).

---

## 3. Puesta en marcha paso a paso

Abre una terminal (PowerShell) y ejecuta:

```powershell
# 1. Descargar el proyecto
git clone https://github.com/gustavoespi27/Maestro-Online-Ecommerce.git
cd Maestro-Online-Ecommerce/Ferreteria

# 2. Definir la contraseña del administrador (se guarda en tu PC, fuera del proyecto)
dotnet user-secrets set "Admin:Password" "Admin123!"

# 3. Confiar en el certificado HTTPS de desarrollo (solo la primera vez; Windows pedirá confirmar → "Sí")
dotnet dev-certs https --trust

# 4. Compilar y arrancar
dotnet run --launch-profile https
```

Cuando la consola muestre `Now listening on: https://localhost:7054`, abre **<https://localhost:7054>** en el navegador.

La primera vez la aplicación **crea sola la base de datos** `FerreteriaDb` en LocalDB, con los roles, el administrador, 8 categorías, 26 productos con fotos y reseñas de ejemplo. No hay que ejecutar migraciones ni scripts SQL: si el modelo cambia, la base se actualiza al arrancar sin perder datos (`Data/SeedData.cs → ActualizarEsquemaAsync`). El aviso *"No hay migrations en el proyecto"* que aparece en la consola es normal.

Para detener la tienda: `Ctrl + C` en la terminal. Para volver a iniciarla: `dotnet run --launch-profile https` desde la carpeta `Ferreteria`.

**Si usas SQL Server en Docker en vez de LocalDB** (Mac/Linux), antes del paso 4:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=TuClave.Segura123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=FerreteriaDb;User Id=sa;Password=TuClave.Segura123;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## 4. Accesos de prueba

| Perfil | Correo | Contraseña | Qué puede hacer |
|---|---|---|---|
| **Administrador** | `admin@ferreteria.cl` | `Admin123!` *(la que definiste en el paso 2)* | Entrar al **Panel admin** (<https://localhost:7054/Admin>): inventario y stock en *Productos*, categorías, pedidos y despachos, clientes, mensajes y resultados de la encuesta. |
| **Cliente** | `cliente@demo.cl` | `Cliente123` | Comprar, ver *Mis pedidos*, editar su perfil y dejar reseñas de productos comprados. **Créala la primera vez** en *Crear cuenta* con esos datos (o con los que quieras). |
| **Invitado** | — | — | Comprar sin registrarse: agrega productos al carrito y en el checkout continúa como invitado. |

- El pago viene en **modo simulado**: al pagar aparece una pantalla con los botones **Aprobar**, **Rechazar** y **Anular** (no se usa ninguna tarjeta). Para probar el Webpay de pruebas de Transbank, ver la [sección 8](#8-probar-el-pago).
- Las contraseñas nuevas deben tener **mínimo 8 caracteres, con mayúscula, minúscula y número**.
- Tras **5 intentos fallidos** la cuenta se bloquea **15 minutos**.
- Estas claves son solo para pruebas locales. En un servidor real define otra contraseña de administrador (sección 7).

---

## 5. Problemas comunes

| Síntoma | Solución |
|---|---|
| `dotnet` no se reconoce como comando | Instala el .NET 8 SDK y **cierra y vuelve a abrir** la terminal (o VS Code). Si persiste, usa la ruta completa: `& "C:\Program Files\dotnet\dotnet.exe" run --launch-profile https`. |
| `dotnet --list-sdks` no muestra ninguna versión 8.0 | Tienes solo el Runtime o una versión distinta: instala el **SDK 8.0**. |
| Error de conexión a SQL Server (*"A network-related or instance-specific error"*) | LocalDB no está instalado o está detenido. Comprueba con `sqllocaldb info`; si aparece `MSSQLLocalDB`, inícialo con `sqllocaldb start MSSQLLocalDB`. |
| El navegador dice *"La conexión no es privada"* | Ejecuta `dotnet dev-certs https --trust`, acepta la ventana de Windows y recarga la página. |
| *"Address already in use"* / puerto ocupado | Ya hay otra copia de la tienda abierta: ciérrala con `Ctrl + C` en su terminal. |
| No recuerdo la contraseña del admin | Revísala con `dotnet user-secrets list`. Si no definiste ninguna, la consola muestra una **contraseña temporal** generada al crear la base. |
| Los formularios dicen *"No pudimos verificar el envío"* | Es la protección contra bots: espera unos segundos después de abrir el formulario y envíalo de nuevo. |
| *"Demasiadas solicitudes"* (error 429) | Se superó el límite de intentos por IP. Espera unos minutos. |
| Quiero empezar con la base limpia | Detén la tienda y ejecuta `dotnet tool install --global dotnet-ef` (solo la primera vez) y `dotnet ef database drop`. Al arrancar de nuevo se vuelve a crear. |

---

## 6. Alternativa: Docker (Windows, Mac o Linux)

Si prefieres **no instalar .NET ni SQL Server**, basta con [Docker Desktop](https://www.docker.com/products/docker-desktop/):

```bash
git clone https://github.com/gustavoespi27/Maestro-Online-Ecommerce.git
cd Maestro-Online-Ecommerce

# 1. Crea tu archivo de configuración y cambia las contraseñas
cp .env.example .env          # en Windows (PowerShell): copy .env.example .env

# 2. Levanta la base de datos y la tienda
docker compose up --build
```

Abre **http://localhost:8080**. La primera vez tarda un par de minutos: descarga SQL Server, crea la base de datos y carga el catálogo de ejemplo.

- Administrador: el correo y la contraseña que pusiste en `.env` (`ADMIN_EMAIL` / `ADMIN_PASSWORD`).
- Para detener: `Ctrl + C` o `docker compose down`. Los datos se guardan en un volumen y se conservan entre reinicios. Para borrarlos: `docker compose down -v`.

> En Mac con chip Apple (M1/M2/M3), activa en Docker Desktop la opción *"Use Rosetta for x86/amd64 emulation"*, porque SQL Server solo publica imágenes para Intel/AMD.

---

## 7. Configuración y secretos

**En el repositorio no hay contraseñas ni llaves.** `appsettings.json` solo trae valores no sensibles y deja los secretos vacíos. Para definirlos hay tres opciones (de mayor a menor prioridad):

1. **Variables de entorno**, usando `__` en lugar de `:` (por ejemplo `Admin__Password`, `Pagos__ApiKeySecret`). Se usan en servidores y en Docker (`.env`).
2. **Secretos de usuario de .NET** (desarrollo): `dotnet user-secrets set "Clave" "valor"`. Se guardan en tu perfil de usuario (`%APPDATA%\Microsoft\UserSecrets` en Windows, `~/.microsoft/usersecrets` en Mac/Linux), fuera de la carpeta del proyecto, así que nunca se suben a GitHub.
3. `appsettings.json` (solo valores públicos).

| Clave | Para qué sirve | Valor por defecto |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Conexión a SQL Server | LocalDB (Windows) |
| `Admin:Email` / `Admin:Password` | Cuenta de administrador que se crea al primer inicio | `admin@ferreteria.cl` / *(vacío → aleatoria en desarrollo)* |
| `Pagos:Modo` | `Simulado` (demo, solo en desarrollo) o `Webpay` | `Simulado` |
| `Pagos:WebpayUrlBase` | `https://webpay3gint.transbank.cl` (pruebas) o `https://webpay3g.transbank.cl` (producción) | Pruebas |
| `Pagos:CodigoComercio` / `Pagos:ApiKeySecret` | Credenciales de Transbank | *(vacío)* |
| `Tienda:*` | Nombre, correo, WhatsApp, dirección, costo de despacho, monto para despacho gratis | Datos ficticios |
| `Seguridad:DirectorioLlaves` | Carpeta persistente para las llaves de cifrado (servidores y Docker) | Perfil del usuario |
| `Seguridad:DetrasDeProxy` / `Seguridad:ProxiesConfiables` | Activar solo si la app está detrás de un proxy inverso con HTTPS | `false` |

El archivo `.env` (Docker) también está en `.gitignore`. Para empezar, copia `.env.example`.

---

## 8. Probar el pago

### Modo simulado (por defecto)

No necesita internet ni llaves. Al pagar se abre una pantalla local con los botones **Aprobar**, **Rechazar** y **Anular**, que reproducen las tres respuestas posibles de Webpay. Por seguridad, la aplicación **se niega a arrancar** con este modo fuera del ambiente de desarrollo.

### Webpay Plus (ambiente de integración de Transbank)

1. Copia el código de comercio y la llave secreta de **Webpay Plus** desde la documentación oficial: <https://www.transbankdevelopers.cl/documentacion/como_empezar#ambientes>
2. Guárdalos como secretos:
   ```bash
   dotnet user-secrets set "Pagos:Modo" "Webpay"
   dotnet user-secrets set "Pagos:CodigoComercio" "<código de comercio>"
   dotnet user-secrets set "Pagos:ApiKeySecret" "<llave secreta>"
   ```
   Con Docker, define `PAGOS_MODO=Webpay`, `WEBPAY_CODIGO_COMERCIO` y `WEBPAY_API_KEY_SECRET` en `.env`.
3. Usa las **tarjetas de prueba** de Transbank (no se hacen cargos reales):

| Tarjeta | Número | CVV | Resultado |
|---|---|---|---|
| VISA crédito | 4051 8856 0044 6623 | 123 | Aprobada |
| Mastercard crédito | 5186 0595 5959 0568 | 123 | Rechazada |

Fecha de expiración: cualquiera futura. Cuando pida autenticación: **RUT 11.111.111-1**, **clave 123**. Lista vigente: <https://www.transbankdevelopers.cl/documentacion/como_empezar#tarjetas-de-prueba>

No necesitas publicar la tienda: Webpay redirige el navegador de vuelta a `localhost`.

---

## 9. Cómo funciona por dentro

### Flujo de compra y pago

```
Cliente              Tienda (ASP.NET Core)                      Transbank
  │ Pagar ─────────────▶ Crea el pedido (Pendiente de pago)
  │                      y reserva el stock (UPDATE atómico)
  │                      POST /transactions ─────────────────────▶
  │                      ◀────────────────────────── { token, url }
  │ ◀─ formulario con token_ws ──────────────────────────────────▶ Pago (tarjeta, RUT, clave)
  │ ◀─────────────── redirige a /Pago/Retorno?token_ws=… ─────────
  │                      PUT /transactions/{token} (commit) ─────▶
  │                      ◀─── { status: AUTHORIZED, response_code: 0, amount, buy_order }
  │                      Valida monto y orden → Pagado
  │                      (rechazado/anulado → devuelve el stock)
  │ ◀── /Pago/Resultado (comprobante)
```

### Reglas de negocio

- **El stock se reserva al crear el pedido** con `UPDATE … WHERE Stock >= cantidad`, así dos personas no pueden comprar la última unidad a la vez. Si el pago falla o se anula, el stock vuelve.
- **El monto siempre sale de la base de datos**, nunca del navegador, y se compara con lo que confirma Transbank.
- **Los cambios de estado del pedido son atómicos**: confirmaciones simultáneas (doble clic, recarga) no pueden anular un pago aprobado ni devolver el stock dos veces.
- **Si se corta la comunicación con Transbank**, se consulta el estado real de la transacción antes de cambiar el pedido.
- **El detalle del pedido guarda el nombre y el precio** del momento de la compra.
- **Precios en pesos chilenos** (`int`, con IVA incluido).

### Recomendaciones (`Services/RecomendacionService.cs`)

Combina, de la señal más fuerte a la más débil:
1. productos **comprados juntos** en pedidos pagados (filtrado colaborativo simple);
2. **categorías de interés**: productos vistos en la sesión y compras anteriores;
3. **popularidad**: lo más vendido, destacados y ofertas (clientes nuevos).

### Chatbot (`Services/ChatbotService.cs`)

Reconoce intenciones por palabras clave (despacho, pagos, devoluciones, seguimiento, horarios, cuenta, cotizaciones…), sin importar tildes ni mayúsculas, y busca productos en el catálogo ("busco un taladro"). No depende de servicios externos. Si no entiende la pregunta, deriva a WhatsApp o al formulario de contacto.

---

## 10. Seguridad

| # | Medida | Cómo se aplica |
|---|---|---|
| 1 | Ocultar API keys | Las llaves de Transbank y la clave del admin solo se leen de secretos de usuario o variables de entorno; nunca llegan al navegador. |
| 2 | Secretos fuera de Git | El historial no contiene secretos; `.env` y archivos locales están en `.gitignore`, y GitHub Actions escanea el historial con **gitleaks** en cada push. |
| 5 | Datos sensibles cifrados | RUT, teléfonos y direcciones se guardan cifrados en la base (ASP.NET Core Data Protection, AES-256 + HMAC). Una copia robada de la base no los revela. |
| 6 | Autenticación forzada | Todo el panel `/Admin` exige el rol Admin mediante una convención global (aunque un controlador olvide `[Authorize]`); "Mis pedidos", reseñas y perfil exigen sesión. |
| 7 | Acceso a registros | Cada pedido solo lo ve su dueño o el admin; las compras de invitado usan un token aleatorio de 256 bits comparado en tiempo constante. |
| 8 | Manipulación de campos | Los formularios no pueden fijar Id, fechas, imágenes, montos ni el carrito (`[BindNever]` y copia explícita de campos permitidos). |
| 9 | Cookies de sesión | `HttpOnly`, `SameSite`, `Secure` y prefijo `__Host-` en producción; la sesión se invalida si cambia la contraseña o el rol. |
| 10 | Contraseñas | Hash PBKDF2-HMAC-SHA512 con sal y 100.000 iteraciones (Identity v3); mínimo 8 caracteres con mayúscula, minúscula y número; bloqueo de 15 min tras 5 intentos. |
| 11 | Rate limiting | Límite global de 300 solicitudes/min por IP y límites específicos para login, registro, formularios, chatbot, pago, encuesta y seguimiento. |
| 12 | Bots | Campo trampa invisible + marca de tiempo firmada en registro, checkout, contacto y encuesta; los pedidos sin pagar se anulan a los 60 min para que nadie bloquee el stock. |
| 13 | Consultas parametrizadas | Todo el acceso a datos usa Entity Framework (consultas parametrizadas); no hay SQL armado con datos del usuario. |
| 14 | Validación de entradas | Anotaciones en todos los formularios, RUT con dígito verificador, listas blancas para opciones, largos máximos y límite de tamaño de las solicitudes. |
| 15 | Sanitización | Razor codifica toda salida HTML; los textos libres se limpian de caracteres de control; el CSV exportado neutraliza fórmulas de Excel. |
| 16 | Archivos | Imágenes subidas: solo JPG/PNG/WEBP/GIF, máx. 2 MB, firma binaria verificada y nombre aleatorio. |
| 17 | Solo datos necesarios | El chatbot y el seguimiento devuelven lo justo; las reseñas muestran nombre e inicial; el registro no revela si un correo ya existe; los errores no muestran detalles internos. |
| 18 | Security headers | CSP con nonce, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`, COOP, CORP y HSTS. |
| 19 | HTTPS | Redirección 308 a HTTPS y HSTS (1 año) en producción; soporte opcional para proxy inverso (`Seguridad:DetrasDeProxy`). |
| 20 | Dependencias | Paquetes al día y sin vulnerabilidades conocidas; **Dependabot** propone actualizaciones y el workflow falla si aparece un paquete vulnerable. |

Los puntos *"key pública para la base de datos"* y *"Row-Level Security"* corresponden a arquitecturas donde el navegador consulta la base directamente (por ejemplo Supabase o Firebase). Aquí el navegador **nunca** se conecta a SQL Server: todo pasa por el servidor, que aplica los permisos de cada registro (punto 7).

> **Importante sobre el cifrado:** las llaves se guardan en tu perfil de usuario (Windows) o en la carpeta indicada en `Seguridad:DirectorioLlaves` (Docker usa un volumen). Si copias la base de datos a otro equipo sin sus llaves, los datos cifrados se mostrarán como "[dato cifrado no disponible]".

---

## 11. Estructura del proyecto

```
├── Dockerfile · docker-compose.yml · .env.example
├── docs/capturas/                     Imágenes de este README
└── Ferreteria/
    ├── Program.cs                     Configuración: EF Core, Identity, sesión, seguridad, pasarela
    ├── appsettings.json               Configuración pública (sin secretos)
    ├── Models/
    │   ├── RutChilenoAttribute.cs     Validación de RUT con dígito verificador
    │   ├── Entidades.cs               Usuario, Categoria, Producto, Pedido, Resena, MensajeContacto, RespuestaEncuesta
    │   ├── Encuesta.cs                Instrumento UTAUT (constructos e ítems)
    │   └── ViewModels/                Modelos de las vistas y formularios
    ├── Data/
    │   ├── ApplicationDbContext.cs    DbContext + Fluent API
    │   ├── SeedData.cs                Creación/actualización de la BD y datos iniciales
    │   └── ResenasDeEjemplo.cs        Reseñas de demostración (rotuladas como ejemplo)
    ├── Services/
    │   ├── Pagos.cs                   Webpay Plus (API REST v1.2) y pago simulado
    │   ├── PedidoService.cs           Pedidos, stock, resultado del pago
    │   ├── Envios.cs                  Tarifas por región, plazos y seguimiento
    │   ├── RecomendacionService.cs    Recomendaciones personalizadas
    │   ├── ChatbotService.cs          Maestro Bot
    │   ├── Seguridad.cs               Límites por IP, cabeceras, CSP y limpieza de textos
    │   ├── CifradoDatos.cs            Cifrado de RUT, teléfonos y direcciones en la base
    │   ├── ProteccionBots.cs          Anti-bots, rol Admin forzado y anulación de pedidos impagos
    │   └── CarritoService.cs · Formato.cs · IdentityErrorDescriberEs.cs
    ├── Controllers/                   Tienda, cuenta, checkout, pago, contacto, chatbot, encuesta, reseñas, info legal, SEO
    ├── Areas/Admin/                   Panel de administración
    ├── Views/                         Vistas Razor
    └── wwwroot/                       CSS, JS, imágenes, manifest
```

---

## 12. Publicar en producción

- Ejecuta con `ASPNETCORE_ENVIRONMENT=Production` y HTTPS.
- Define por variables de entorno `ConnectionStrings__DefaultConnection`, `Admin__Password`, `Pagos__Modo=Webpay`, `Pagos__WebpayUrlBase=https://webpay3g.transbank.cl` y las credenciales de producción que entrega Transbank al afiliar el comercio.
- Pendiente para una tienda real: confirmación de correo, recuperación de contraseña, envío de comprobantes por correo y reemplazar las reseñas de ejemplo por reseñas reales.

---

## 13. Créditos

- Las fotografías de productos y categorías provienen de **Wikimedia Commons** bajo licencias libres (Creative Commons y dominio público). El autor y la licencia de cada imagen están en la página **Créditos de imágenes** del sitio (`/Info/Creditos`) y en `Ferreteria/wwwroot/img/creditos-imagenes.json`.
- El logo de *El Maestro Online* es una creación original del proyecto. Las marcas de los productos (Bosch, Makita, Stanley, etc.) pertenecen a sus titulares y se mencionan solo para identificar los productos.
- Proyecto académico INACAP · Asignatura Adopción de Tecnologías (TIEL25).
