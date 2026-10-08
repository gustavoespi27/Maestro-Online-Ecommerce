using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Ferreteria.Services;

public static class Formato
{
    private static readonly CultureInfo Chile = CultureInfo.GetCultureInfo("es-CL");

    /// <summary>Formatea un monto en pesos chilenos: 12990 → "$12.990".</summary>
    public static string Clp(this int monto) => "$" + monto.ToString("N0", Chile);

    /// <summary>Devuelve el texto de [Display(Name=...)] de un enum.</summary>
    public static string Nombre(this Enum valor)
    {
        var campo = valor.GetType().GetField(valor.ToString());
        return campo?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? valor.ToString();
    }
}
