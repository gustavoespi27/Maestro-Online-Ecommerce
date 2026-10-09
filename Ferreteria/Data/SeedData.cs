using Ferreteria.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ferreteria.Data;

/// <summary>
/// Crea los roles, el usuario administrador y un catálogo de ejemplo la primera vez que corre la app.
/// </summary>
public static class SeedData
{
    public const string RolAdmin = "Admin";
    public const string RolCliente = "Cliente";

    /// <summary>Productos de ejemplo que parten "en oferta" (SKU → precio anterior).</summary>
    private static readonly Dictionary<string, int> PreciosAnterioresEjemplo = new()
    {
        ["HM-001"] = 15990,
        ["HM-002"] = 11990,
        ["HE-001"] = 69990,
        ["HE-002"] = 99990,
        ["HE-003"] = 59990,
        ["PI-001"] = 19990,
        ["GA-003"] = 42990,
        ["EL-002"] = 2990,
        ["JA-001"] = 16990,
    };

    public static async Task InicializarAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // Si existen migrations se aplican; si no, se crea la BD directamente desde el modelo.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogWarning("No hay migrations en el proyecto: se crea la base de datos con EnsureCreated(). " +
                              "Ejecuta 'dotnet ef migrations add Inicial' para usar migrations.");
            await db.Database.EnsureCreatedAsync();
            await ActualizarEsquemaAsync(db);
        }

        await CrearRolesYAdminAsync(services, config);
        await CrearCatalogoAsync(db);
        await ActualizarCatalogoEjemploAsync(db, services.GetRequiredService<IWebHostEnvironment>());
        await CrearResenasEjemploAsync(db);
        await CifrarDatosExistentesAsync(db);
    }

    /// <summary>
    /// Cifra los datos personales que quedaron guardados en texto plano antes de activar el cifrado.
    /// Se buscan con SQL directo (sin pasar por el convertidor) y se vuelven a guardar: al guardar, EF los cifra.
    /// </summary>
    private static async Task CifrarDatosExistentesAsync(ApplicationDbContext db)
    {
        // Consultas fijas (sin datos externos): buscan valores que aún no tienen el prefijo "enc:"
        var usuarios = await db.Database.SqlQuery<string>($"""
            SELECT Id AS Value FROM AspNetUsers
            WHERE (Rut IS NOT NULL AND Rut NOT LIKE 'enc:%')
               OR (Direccion IS NOT NULL AND Direccion NOT LIKE 'enc:%')
               OR (PhoneNumber IS NOT NULL AND PhoneNumber NOT LIKE 'enc:%')
            """).ToListAsync();
        foreach (var u in await db.Users.Where(u => usuarios.Contains(u.Id)).ToListAsync())
        {
            var e = db.Entry(u);
            e.Property(x => x.Rut).IsModified = u.Rut is not null;
            e.Property(x => x.Direccion).IsModified = u.Direccion is not null;
            e.Property(x => x.PhoneNumber).IsModified = u.PhoneNumber is not null;
        }

        var pedidos = await db.Database.SqlQuery<int>($"""
            SELECT Id AS Value FROM Pedidos
            WHERE (Telefono IS NOT NULL AND Telefono NOT LIKE 'enc:%')
               OR (Direccion IS NOT NULL AND Direccion NOT LIKE 'enc:%')
            """).ToListAsync();
        foreach (var p in await db.Pedidos.Where(p => pedidos.Contains(p.Id)).ToListAsync())
        {
            var e = db.Entry(p);
            e.Property(x => x.Telefono).IsModified = p.Telefono is not null;
            e.Property(x => x.Direccion).IsModified = p.Direccion is not null;
        }

        var mensajes = await db.Database.SqlQuery<int>($"""
            SELECT Id AS Value FROM MensajesContacto WHERE Telefono IS NOT NULL AND Telefono NOT LIKE 'enc:%'
            """).ToListAsync();
        foreach (var m in await db.MensajesContacto.Where(m => mensajes.Contains(m.Id)).ToListAsync())
            db.Entry(m).Property(x => x.Telefono).IsModified = true;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Reseñas de demostración para que el catálogo no se vea vacío. Se rotulan como "Reseña de ejemplo"
    /// (no como compra verificada) y no se informan a Google como calificaciones reales.
    /// </summary>
    private static async Task CrearResenasEjemploAsync(ApplicationDbContext db)
    {
        if (await db.Resenas.AnyAsync(r => r.EsEjemplo)) return;

        var productos = await db.Productos.ToDictionaryAsync(p => p.Sku, p => p.Id);
        var hoy = DateTime.Today;
        foreach (var (sku, autor, estrellas, comentario, diasAtras) in ResenasDeEjemplo.Lista)
        {
            if (!productos.TryGetValue(sku, out var productoId)) continue;
            db.Resenas.Add(new Resena
            {
                ProductoId = productoId,
                NombreAutor = autor,
                EsEjemplo = true,
                Calificacion = estrellas,
                Comentario = comentario,
                Fecha = hoy.AddDays(-diasAtras).AddHours(9 + diasAtras % 10).AddMinutes(diasAtras * 7 % 60)
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// EnsureCreated() no modifica una BD que ya existe. Aquí se agregan las columnas nuevas
    /// sin borrar datos (equivalente a una migration pequeña).
    /// </summary>
    private static async Task ActualizarEsquemaAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "IF COL_LENGTH('Productos', 'PrecioAnterior') IS NULL ALTER TABLE Productos ADD PrecioAnterior int NULL;");

        // Consentimiento legal del usuario
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('AspNetUsers', 'FechaAceptacionTerminos') IS NULL ALTER TABLE AspNetUsers ADD FechaAceptacionTerminos datetime2 NULL;
            IF COL_LENGTH('AspNetUsers', 'AceptaComunicaciones') IS NULL ALTER TABLE AspNetUsers ADD AceptaComunicaciones bit NOT NULL CONSTRAINT DF_AspNetUsers_AceptaComunicaciones DEFAULT 0;
            """);

        // Logística del pedido
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Pedidos', 'Region') IS NULL ALTER TABLE Pedidos ADD Region nvarchar(60) NULL;
            IF COL_LENGTH('Pedidos', 'FechaEstimadaEntrega') IS NULL ALTER TABLE Pedidos ADD FechaEstimadaEntrega datetime2 NULL;
            IF COL_LENGTH('Pedidos', 'Transportista') IS NULL ALTER TABLE Pedidos ADD Transportista nvarchar(40) NULL;
            IF COL_LENGTH('Pedidos', 'CodigoSeguimiento') IS NULL ALTER TABLE Pedidos ADD CodigoSeguimiento nvarchar(40) NULL;
            IF COL_LENGTH('Pedidos', 'FechaDespacho') IS NULL ALTER TABLE Pedidos ADD FechaDespacho datetime2 NULL;
            IF COL_LENGTH('Pedidos', 'FechaEntrega') IS NULL ALTER TABLE Pedidos ADD FechaEntrega datetime2 NULL;
            """);

        // Tablas nuevas: contacto, reseñas y encuesta UTAUT
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('MensajesContacto') IS NULL
            CREATE TABLE MensajesContacto (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MensajesContacto PRIMARY KEY,
                Nombre nvarchar(100) NOT NULL,
                Email nvarchar(120) NOT NULL,
                Telefono nvarchar(20) NULL,
                Asunto int NOT NULL,
                OrdenCompra nvarchar(26) NULL,
                Mensaje nvarchar(2000) NOT NULL,
                Fecha datetime2 NOT NULL,
                Atendido bit NOT NULL);

            IF OBJECT_ID('Resenas') IS NULL
            BEGIN
                CREATE TABLE Resenas (
                    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Resenas PRIMARY KEY,
                    ProductoId int NOT NULL CONSTRAINT FK_Resenas_Productos_ProductoId REFERENCES Productos(Id) ON DELETE CASCADE,
                    UsuarioId nvarchar(450) NULL CONSTRAINT FK_Resenas_AspNetUsers_UsuarioId REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                    NombreAutor nvarchar(60) NULL,
                    EsEjemplo bit NOT NULL CONSTRAINT DF_Resenas_EsEjemplo DEFAULT 0,
                    Calificacion int NOT NULL,
                    Comentario nvarchar(600) NULL,
                    Fecha datetime2 NOT NULL);
                CREATE UNIQUE INDEX IX_Resenas_ProductoId_UsuarioId ON Resenas(ProductoId, UsuarioId) WHERE UsuarioId IS NOT NULL;
                CREATE INDEX IX_Resenas_UsuarioId ON Resenas(UsuarioId);
            END

            IF OBJECT_ID('RespuestasEncuesta') IS NULL
            CREATE TABLE RespuestasEncuesta (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RespuestasEncuesta PRIMARY KEY,
                Fecha datetime2 NOT NULL,
                RangoEdad nvarchar(20) NULL,
                Genero nvarchar(20) NULL,
                FrecuenciaCompraOnline nvarchar(30) NULL,
                Perfil nvarchar(30) NULL,
                ED1 int NOT NULL, ED2 int NOT NULL, ED3 int NOT NULL,
                EE1 int NOT NULL, EE2 int NOT NULL, EE3 int NOT NULL,
                IS1 int NOT NULL, IS2 int NOT NULL, IS3 int NOT NULL,
                CF1 int NOT NULL, CF2 int NOT NULL, CF3 int NOT NULL,
                IU1 int NOT NULL, IU2 int NOT NULL, IU3 int NOT NULL,
                Comentario nvarchar(500) NULL);
            """);

        // Compras como invitado: el pedido puede no tener usuario
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Pedidos', 'EmailInvitado') IS NULL ALTER TABLE Pedidos ADD EmailInvitado nvarchar(120) NULL;
            IF COL_LENGTH('Pedidos', 'TokenAcceso') IS NULL ALTER TABLE Pedidos ADD TokenAcceso nvarchar(64) NULL;
            IF COLUMNPROPERTY(OBJECT_ID('Pedidos'), 'UsuarioId', 'AllowsNull') = 0
            BEGIN
                ALTER TABLE Pedidos DROP CONSTRAINT FK_Pedidos_AspNetUsers_UsuarioId;
                DROP INDEX IX_Pedidos_UsuarioId ON Pedidos;
                ALTER TABLE Pedidos ALTER COLUMN UsuarioId nvarchar(450) NULL;
                CREATE INDEX IX_Pedidos_UsuarioId ON Pedidos(UsuarioId);
                ALTER TABLE Pedidos ADD CONSTRAINT FK_Pedidos_AspNetUsers_UsuarioId FOREIGN KEY (UsuarioId) REFERENCES AspNetUsers(Id);
            END
            """);

        // Columnas cifradas: el texto cifrado es más largo que el original
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('AspNetUsers', 'Rut') < 1000 ALTER TABLE AspNetUsers ALTER COLUMN Rut nvarchar(500) NULL;
            IF COL_LENGTH('AspNetUsers', 'Direccion') < 1000 ALTER TABLE AspNetUsers ALTER COLUMN Direccion nvarchar(500) NULL;
            IF COL_LENGTH('Pedidos', 'Telefono') < 1000 ALTER TABLE Pedidos ALTER COLUMN Telefono nvarchar(500) NULL;
            IF COL_LENGTH('Pedidos', 'Direccion') < 1000 ALTER TABLE Pedidos ALTER COLUMN Direccion nvarchar(500) NULL;
            IF COL_LENGTH('MensajesContacto', 'Telefono') < 1000 ALTER TABLE MensajesContacto ALTER COLUMN Telefono nvarchar(500) NULL;
            """);

        // Reseñas de ejemplo: sin usuario y con nombre visible
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Resenas', 'NombreAutor') IS NULL ALTER TABLE Resenas ADD NombreAutor nvarchar(60) NULL;
            IF COL_LENGTH('Resenas', 'EsEjemplo') IS NULL ALTER TABLE Resenas ADD EsEjemplo bit NOT NULL CONSTRAINT DF_Resenas_EsEjemplo DEFAULT 0;
            IF COLUMNPROPERTY(OBJECT_ID('Resenas'), 'UsuarioId', 'AllowsNull') = 0
            BEGIN
                ALTER TABLE Resenas DROP CONSTRAINT FK_Resenas_AspNetUsers_UsuarioId;
                DROP INDEX IX_Resenas_ProductoId_UsuarioId ON Resenas;
                DROP INDEX IX_Resenas_UsuarioId ON Resenas;
                ALTER TABLE Resenas ALTER COLUMN UsuarioId nvarchar(450) NULL;
                CREATE UNIQUE INDEX IX_Resenas_ProductoId_UsuarioId ON Resenas(ProductoId, UsuarioId) WHERE UsuarioId IS NOT NULL;
                CREATE INDEX IX_Resenas_UsuarioId ON Resenas(UsuarioId);
                ALTER TABLE Resenas ADD CONSTRAINT FK_Resenas_AspNetUsers_UsuarioId FOREIGN KEY (UsuarioId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE;
            END
            """);
    }

    /// <summary>
    /// Los productos de ejemplo creados con la versión anterior usaban un dibujo genérico por categoría.
    /// Se les asigna su foto real y su oferta inicial. Los productos que ya tienen otra imagen no se tocan.
    /// </summary>
    private static async Task ActualizarCatalogoEjemploAsync(ApplicationDbContext db, IWebHostEnvironment env)
    {
        var antiguos = await db.Productos
            .Where(p => p.ImagenUrl != null && p.ImagenUrl.StartsWith("/img/productos/") && p.ImagenUrl.EndsWith(".svg"))
            .ToListAsync();
        foreach (var p in antiguos)
        {
            var archivo = $"{p.Sku.ToLowerInvariant()}.jpg";
            if (!File.Exists(Path.Combine(env.WebRootPath, "img", "productos", archivo))) continue;
            p.ImagenUrl = $"/img/productos/{archivo}";
            if (p.PrecioAnterior is null && PreciosAnterioresEjemplo.TryGetValue(p.Sku, out var antes) && antes > p.Precio)
                p.PrecioAnterior = antes;
        }
        await db.SaveChangesAsync();
    }

    private static async Task CrearRolesYAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();

        foreach (var rol in new[] { RolAdmin, RolCliente })
        {
            if (!await roleManager.RoleExistsAsync(rol))
                await roleManager.CreateAsync(new IdentityRole(rol));
        }

        var email = config["Admin:Email"] ?? "admin@ferreteria.cl";
        if (await userManager.FindByEmailAsync(email) is not null) return;

        // La contraseña del administrador nunca va en el repositorio: secretos de usuario o variable de entorno Admin__Password
        var password = config["Admin:Password"];
        var env = services.GetRequiredService<IWebHostEnvironment>();
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException("Configura Admin:Password (variable de entorno Admin__Password) antes de publicar.");
            // En desarrollo se genera una contraseña aleatoria y se muestra una sola vez en la consola
            password = "Mo-" + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9)).Replace('+', 'x').Replace('/', 'y') + "9a";
            services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData").LogWarning(
                "Se creó el administrador {Email} con la contraseña temporal: {Password}  (cámbiala o define Admin:Password)", email, password);
        }

        var admin = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "Administrador"
        };
        var resultado = await userManager.CreateAsync(admin, password);
        if (resultado.Succeeded)
            await userManager.AddToRoleAsync(admin, RolAdmin);
        else
            throw new InvalidOperationException("No se pudo crear el administrador: " +
                string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }

    private static async Task CrearCatalogoAsync(ApplicationDbContext db)
    {
        if (await db.Categorias.AnyAsync()) return;

        var categorias = new Dictionary<string, Categoria>
        {
            ["manuales"] = new() { Nombre = "Herramientas manuales", Icono = "manuales", Descripcion = "Martillos, destornilladores, alicates y llaves." },
            ["electricas"] = new() { Nombre = "Herramientas eléctricas", Icono = "electricas", Descripcion = "Taladros, esmeriles, sierras y atornilladores." },
            ["pinturas"] = new() { Nombre = "Pinturas", Icono = "pinturas", Descripcion = "Látex, esmaltes, barnices y accesorios para pintar." },
            ["gasfiteria"] = new() { Nombre = "Gasfitería", Icono = "gasfiteria", Descripcion = "Llaves, cañerías, fittings y sellantes." },
            ["electricidad"] = new() { Nombre = "Electricidad", Icono = "electricidad", Descripcion = "Cables, enchufes, interruptores y ampolletas." },
            ["fijaciones"] = new() { Nombre = "Fijaciones", Icono = "fijaciones", Descripcion = "Tornillos, clavos, tarugos y pernos." },
            ["construccion"] = new() { Nombre = "Construcción", Icono = "construccion", Descripcion = "Cemento, adhesivos, yeso y materiales de obra." },
            ["jardin"] = new() { Nombre = "Jardín", Icono = "jardin", Descripcion = "Mangueras, palas, tijeras de podar y riego." }
        };
        db.Categorias.AddRange(categorias.Values);

        Producto P(string cat, string sku, string nombre, string marca, int precio, int stock, string desc, bool destacado = false) => new()
        {
            Categoria = categorias[cat],
            Sku = sku,
            Nombre = nombre,
            Marca = marca,
            Precio = precio,
            PrecioAnterior = PreciosAnterioresEjemplo.TryGetValue(sku, out var antes) ? antes : null,
            Stock = stock,
            Descripcion = desc,
            Destacado = destacado,
            ImagenUrl = $"/img/productos/{sku.ToLowerInvariant()}.jpg"
        };

        db.Productos.AddRange(
            P("manuales", "HM-001", "Martillo carpintero 16 oz", "Stanley", 12990, 40, "Martillo de uña con mango de fibra de vidrio antivibración y cabeza de acero forjado.", true),
            P("manuales", "HM-002", "Set destornilladores 6 piezas", "Truper", 8990, 55, "Puntas planas y Phillips de acero cromo-vanadio con mango ergonómico."),
            P("manuales", "HM-003", "Alicate universal 8\"", "Bahco", 10490, 30, "Alicate con corte lateral y mango aislado bimaterial."),
            P("manuales", "HM-004", "Huincha de medir 5 m", "Stanley", 5990, 80, "Cinta métrica con freno y gancho magnético."),
            P("manuales", "HM-005", "Llave ajustable 10\"", "Bahco", 14990, 3, "Llave francesa de acero con escala en milímetros."),
            P("electricas", "HE-001", "Taladro percutor 650 W", "Bosch", 54990, 15, "Taladro percutor con mandril de 13 mm, velocidad variable y reversa.", true),
            P("electricas", "HE-002", "Atornillador inalámbrico 12 V", "Makita", 79990, 10, "Incluye 2 baterías de litio, cargador y maletín.", true),
            P("electricas", "HE-003", "Esmeril angular 4 1/2\" 850 W", "DeWalt", 49990, 12, "Esmeril compacto con protector ajustable sin herramientas."),
            P("electricas", "HE-004", "Sierra circular 7 1/4\" 1400 W", "Bosch", 89990, 4, "Corte hasta 65 mm, incluye disco de 24 dientes."),
            P("pinturas", "PI-001", "Látex interior blanco 1 galón", "Tricolor", 15990, 60, "Pintura látex lavable, terminación mate, rendimiento 35 m² por galón.", true),
            P("pinturas", "PI-002", "Esmalte sintético negro 1/4 gal", "Sipa", 7490, 35, "Esmalte brillante para metal y madera."),
            P("pinturas", "PI-003", "Rodillo antigota 23 cm", "Tricolor", 4990, 70, "Rodillo de lana sintética con mango reforzado."),
            P("pinturas", "PI-004", "Set de brochas 3 piezas", "Truper", 3990, 90, "Brochas de 1\", 2\" y 3\" con cerda natural."),
            P("gasfiteria", "GA-001", "Llave de paso 1/2\" bronce", "Nibsa", 6990, 45, "Llave de bola de bronce para agua fría y caliente."),
            P("gasfiteria", "GA-002", "Cinta teflón 12 mm x 10 m", "Genérico", 590, 300, "Cinta sellante para uniones roscadas."),
            P("gasfiteria", "GA-003", "Monomando lavaplatos cromado", "Stretto", 32990, 8, "Grifería monomando con caño giratorio."),
            P("electricidad", "EL-001", "Cable eléctrico 1,5 mm rollo 100 m", "Madeco", 39990, 20, "Cable THHN para instalaciones domiciliarias."),
            P("electricidad", "EL-002", "Ampolleta LED 9 W luz cálida", "Philips", 1990, 150, "Equivalente a 60 W, rosca E27, vida útil 15.000 horas.", true),
            P("electricidad", "EL-003", "Enchufe doble embutido", "Bticino", 3490, 65, "Enchufe 10 A con tapa blanca."),
            P("fijaciones", "FI-001", "Tornillo volcanita 6x1\" (100 u.)", "Mamut", 2490, 200, "Tornillos punta fina fosfatados para perfiles de volcanita."),
            P("fijaciones", "FI-002", "Clavo corriente 2\" (1 kg)", "Inchalam", 2290, 120, "Clavo de acero para carpintería general."),
            P("fijaciones", "FI-003", "Tarugo plástico 8 mm (50 u.)", "Fischer", 1990, 2, "Tarugo de nylon para muros de hormigón y ladrillo."),
            P("construccion", "CO-001", "Cemento especial 25 kg", "Melón", 6490, 100, "Cemento para hormigones, morteros y estucos."),
            P("construccion", "CO-002", "Adhesivo cerámico 25 kg", "Bekron", 7990, 50, "Pegamento en polvo para cerámicas de piso y muro."),
            P("jardin", "JA-001", "Manguera reforzada 15 m", "Tramontina", 12990, 25, "Manguera de PVC con malla, incluye conectores.", true),
            P("jardin", "JA-002", "Tijera de podar bypass", "Tramontina", 9990, 18, "Tijera con hoja de acero al carbono y resorte.")
        );

        await db.SaveChangesAsync();
    }
}
