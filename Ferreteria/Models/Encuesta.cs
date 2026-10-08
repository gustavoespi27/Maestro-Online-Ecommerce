namespace Ferreteria.Models;

/// <summary>Un ítem (afirmación) de la encuesta UTAUT, con escala Likert de 1 a 5.</summary>
public record ItemEncuesta(string Codigo, string Texto);

/// <summary>Un constructo del modelo UTAUT (Venkatesh et al., 2003) con sus ítems.</summary>
public record ConstructoUtaut(string Sigla, string Nombre, string Descripcion, ItemEncuesta[] Items);

/// <summary>
/// Instrumento de aceptación tecnológica basado en UTAUT (Venkatesh, Morris, Davis &amp; Davis, 2003),
/// adaptado al sitio El Maestro Online. Los códigos (ED1, EE2...) coinciden con las columnas de <see cref="RespuestaEncuesta"/>.
/// </summary>
public static class EncuestaUtaut
{
    public static readonly string[] Escala =
        { "Muy en desacuerdo", "En desacuerdo", "Ni de acuerdo ni en desacuerdo", "De acuerdo", "Muy de acuerdo" };

    public static readonly ConstructoUtaut[] Constructos =
    {
        new("ED", "Expectativa de Desempeño",
            "Grado en que la persona cree que usar el sitio le ayuda a lograr su objetivo (comprar mejor y más rápido).",
            new ItemEncuesta[]
            {
                new("ED1", "Comprar en El Maestro Online me permitiría conseguir mis materiales y herramientas más rápido que en una tienda física."),
                new("ED2", "El sitio me ayuda a comparar precios y encontrar ofertas convenientes."),
                new("ED3", "Las recomendaciones y la información de los productos me ayudan a elegir lo que necesito."),
            }),
        new("EE", "Expectativa de Esfuerzo",
            "Grado de facilidad asociado al uso del sitio.",
            new ItemEncuesta[]
            {
                new("EE1", "Me resulta fácil encontrar productos con el buscador, las categorías y los filtros."),
                new("EE2", "El proceso de compra (carrito, despacho y pago) es claro y sencillo."),
                new("EE3", "El sitio se ve y funciona bien en mi celular."),
            }),
        new("IS", "Influencia Social",
            "Grado en que la persona percibe que otros importantes para ella creen que debería usar el sitio.",
            new ItemEncuesta[]
            {
                new("IS1", "Las personas cuyas opiniones valoro (familia, colegas, maestros) usarían una ferretería online como esta."),
                new("IS2", "Las reseñas de otros compradores influyen en mi decisión de compra."),
                new("IS3", "Recomendaría El Maestro Online a un amigo o colega."),
            }),
        new("CF", "Condiciones Facilitadoras",
            "Grado en que la persona cree que existen los recursos y el apoyo para usar el sitio.",
            new ItemEncuesta[]
            {
                new("CF1", "Tengo los medios necesarios para comprar online (internet, tarjeta de débito o crédito)."),
                new("CF2", "El pago con Webpay y las políticas visibles (privacidad, devoluciones) me dan confianza para comprar."),
                new("CF3", "Si tengo un problema, el chatbot, WhatsApp y el formulario de contacto me ayudarían a resolverlo."),
            }),
        new("IU", "Intención de Uso",
            "Disposición de la persona a usar el sitio en el futuro (variable dependiente del modelo).",
            new ItemEncuesta[]
            {
                new("IU1", "Tengo intención de comprar en El Maestro Online en los próximos meses."),
                new("IU2", "Preferiría comprar mis productos de ferretería en este sitio antes que ir a una tienda física."),
                new("IU3", "Volvería a usar el sitio para futuras compras."),
            }),
    };

    public static readonly string[] RangosEdad = { "18 a 24", "25 a 34", "35 a 44", "45 a 54", "55 o más" };
    public static readonly string[] Generos = { "Femenino", "Masculino", "Otro", "Prefiero no decir" };
    public static readonly string[] Frecuencias = { "Nunca", "1 vez al año", "Cada 2 a 3 meses", "1 vez al mes", "Más de 1 vez al mes" };
    public static readonly string[] Perfiles = { "Particular / hogar", "Maestro o técnico", "Contratista o empresa", "Estudiante" };

    /// <summary>Lee el valor de un ítem por su código (ED1, EE2...).</summary>
    public static int Valor(RespuestaEncuesta r, string codigo) =>
        (int)(typeof(RespuestaEncuesta).GetProperty(codigo)!.GetValue(r) ?? 0);

    public static void Asignar(RespuestaEncuesta r, string codigo, int valor) =>
        typeof(RespuestaEncuesta).GetProperty(codigo)!.SetValue(r, valor);

    public static double Promedio(RespuestaEncuesta r, ConstructoUtaut c) => c.Items.Average(i => Valor(r, i.Codigo));
}
