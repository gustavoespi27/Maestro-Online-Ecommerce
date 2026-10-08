namespace Ferreteria.Services;

/// <summary>Tarifa de despacho de una región de Chile.</summary>
public record TarifaRegion(string Nombre, int Costo, int DiasMin, int DiasMax);

/// <summary>
/// Logística: tarifas por región, cálculo de la fecha estimada de entrega y códigos de seguimiento.
/// Las tarifas son referenciales (tienda ficticia). El despacho gratis sobre el monto configurado aplica
/// a la Región Metropolitana y regiones cercanas; las regiones extremas pagan un costo reducido.
/// </summary>
public static class Envios
{
    public const string RegionPorDefecto = "Metropolitana de Santiago";
    public const int DiasPreparacionRetiro = 1;

    public static readonly IReadOnlyList<TarifaRegion> Regiones = new List<TarifaRegion>
    {
        new("Arica y Parinacota", 9990, 5, 7),
        new("Tarapacá", 9990, 5, 7),
        new("Antofagasta", 8990, 4, 6),
        new("Atacama", 7990, 3, 5),
        new("Coquimbo", 6990, 3, 4),
        new("Valparaíso", 5490, 2, 3),
        new("Metropolitana de Santiago", 4990, 1, 2),
        new("Libertador General Bernardo O'Higgins", 5490, 2, 3),
        new("Maule", 5990, 2, 4),
        new("Ñuble", 6490, 3, 4),
        new("Biobío", 6490, 3, 4),
        new("La Araucanía", 6990, 3, 5),
        new("Los Ríos", 7490, 3, 5),
        new("Los Lagos", 7990, 4, 6),
        new("Aysén del General Carlos Ibáñez del Campo", 12990, 6, 9),
        new("Magallanes y de la Antártica Chilena", 12990, 6, 9),
    };

    /// <summary>Regiones extremas: el despacho gratis no aplica, pero se descuenta el 50%.</summary>
    private static readonly HashSet<string> RegionesExtremas = new()
    {
        "Arica y Parinacota", "Tarapacá", "Aysén del General Carlos Ibáñez del Campo", "Magallanes y de la Antártica Chilena"
    };

    public static TarifaRegion Tarifa(string? region) =>
        Regiones.FirstOrDefault(r => r.Nombre == region) ?? Regiones.First(r => r.Nombre == RegionPorDefecto);

    public static bool EsRegionValida(string? region) => Regiones.Any(r => r.Nombre == region);

    /// <summary>Costo de despacho a una región considerando el beneficio de despacho gratis.</summary>
    public static int Costo(string? region, int subtotal, int gratisDesde)
    {
        var tarifa = Tarifa(region);
        if (subtotal < gratisDesde) return tarifa.Costo;
        return RegionesExtremas.Contains(tarifa.Nombre) ? tarifa.Costo / 2 : 0;
    }

    /// <summary>Suma días hábiles (lunes a sábado; el domingo no se despacha).</summary>
    public static DateTime SumarDiasHabiles(DateTime desde, int dias)
    {
        var fecha = desde.Date;
        // Las compras después de las 14:00 se procesan al día siguiente
        if (desde.Hour >= 14) dias++;
        while (dias > 0)
        {
            fecha = fecha.AddDays(1);
            if (fecha.DayOfWeek != DayOfWeek.Sunday) dias--;
        }
        return fecha;
    }

    public static (DateTime Desde, DateTime Hasta) RangoEntrega(string? region, DateTime fechaCompra)
    {
        var t = Tarifa(region);
        return (SumarDiasHabiles(fechaCompra, t.DiasMin), SumarDiasHabiles(fechaCompra, t.DiasMax));
    }

    public static readonly string[] Transportistas = { "Starken", "Chilexpress", "Blue Express", "Correos de Chile", "Flota propia" };

    /// <summary>Código de seguimiento con el formato MO + fecha + aleatorio (ej: MO2610081234567).</summary>
    public static string GenerarCodigoSeguimiento() =>
        $"MO{DateTime.Now:yyMMdd}{Random.Shared.Next(1000000, 9999999)}";
}
