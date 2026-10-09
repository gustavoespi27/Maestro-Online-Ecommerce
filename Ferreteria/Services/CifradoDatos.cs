using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ferreteria.Services;

/// <summary>
/// Cifrado de datos personales en la base de datos (RUT, teléfonos y direcciones).
/// Usa ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256, con rotación automática de llaves).
/// Los valores cifrados se guardan con el prefijo "enc:"; los valores antiguos sin prefijo se leen tal cual
/// y se cifran al arrancar la aplicación (ver SeedData.CifrarDatosExistentesAsync).
/// Si alguien obtiene una copia de la base de datos, sin las llaves no puede leer estos datos.
/// </summary>
public static class CifradoDatos
{
    public const string Prefijo = "enc:";
    public const string Proposito = "ElMaestro.DatosPersonales.v1";
    public const string NoDisponible = "[dato cifrado no disponible]";
    /// <summary>Largo de las columnas cifradas (el texto cifrado es más largo que el original).</summary>
    public const int LargoColumna = 500;

    public static ValueConverter<string?, string?> Convertidor(IDataProtector protector) => new(
        v => Cifrar(protector, v),
        v => Descifrar(protector, v));

    // Siempre se cifra (aunque el texto empiece con "enc:"): los valores leídos ya vienen descifrados
    public static string? Cifrar(IDataProtector protector, string? valor) =>
        valor is null ? null : Prefijo + protector.Protect(valor);

    public static string? Descifrar(IDataProtector protector, string? valor)
    {
        if (valor is null || !valor.StartsWith(Prefijo)) return valor; // nulo o dato antiguo aún sin cifrar
        try
        {
            return protector.Unprotect(valor[Prefijo.Length..]);
        }
        catch (CryptographicException)
        {
            // Llaves distintas (por ejemplo, la base se copió a otro equipo sin sus llaves)
            return NoDisponible;
        }
    }
}
