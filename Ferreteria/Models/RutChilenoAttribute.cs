using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Ferreteria.Models;

/// <summary>
/// Valida un RUT chileno: formato 12345678-9 (sin puntos) y dígito verificador correcto (módulo 11).
/// Un valor vacío es válido (el RUT es opcional); para hacerlo obligatorio se combina con [Required].
/// </summary>
public partial class RutChilenoAttribute : ValidationAttribute
{
    public RutChilenoAttribute() : base("RUT inválido. Usa el formato 12345678-9 y revisa el dígito verificador.") { }

    [GeneratedRegex(@"^(\d{7,8})-([\dkK])$")]
    private static partial Regex Formato();

    public override bool IsValid(object? value)
    {
        if (value is not string rut || string.IsNullOrWhiteSpace(rut)) return true;
        var m = Formato().Match(rut.Trim());
        if (!m.Success) return false;
        return DigitoVerificador(m.Groups[1].Value) == char.ToUpperInvariant(m.Groups[2].Value[0]);
    }

    public static char DigitoVerificador(string cuerpo)
    {
        int suma = 0, factor = 2;
        for (var i = cuerpo.Length - 1; i >= 0; i--)
        {
            suma += (cuerpo[i] - '0') * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }
        var resto = 11 - suma % 11;
        return resto switch { 11 => '0', 10 => 'K', _ => (char)('0' + resto) };
    }
}
