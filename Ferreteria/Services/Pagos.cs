using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Ferreteria.Services;

/// <summary>Configuración leída desde appsettings.json → sección "Pagos".</summary>
public class OpcionesPago
{
    /// <summary>"Webpay" (Transbank) o "Simulado" (sin internet, solo en desarrollo).</summary>
    public string Modo { get; set; } = "Simulado";
    public string WebpayUrlBase { get; set; } = "https://webpay3gint.transbank.cl";

    /// <summary>Código de comercio (Tbk-Api-Key-Id). Se configura con secretos de usuario o variables de entorno, nunca en el repositorio.</summary>
    public string CodigoComercio { get; set; } = "";

    /// <summary>Llave secreta (Tbk-Api-Key-Secret). Se configura con secretos de usuario o variables de entorno, nunca en el repositorio.</summary>
    public string ApiKeySecret { get; set; } = "";
}

public record TransaccionCreada(string Token, string Url);

public class ResultadoPago
{
    /// <summary>No se pudo saber el resultado (sin comunicación con la pasarela). El pedido no debe cambiar de estado.</summary>
    public const string ErrorComunicacion = "ERROR_COMUNICACION";

    public bool Aprobado { get; set; }
    public int Monto { get; set; }
    public string? OrdenCompra { get; set; }
    public string? CodigoAutorizacion { get; set; }
    public string? UltimosDigitosTarjeta { get; set; }
    public string? TipoPago { get; set; }
    public int? Cuotas { get; set; }
    public int CodigoRespuesta { get; set; }
    public string? Estado { get; set; }
}

/// <summary>Abstracción de la pasarela de pago, para poder cambiar Webpay por otra (o por el simulador).</summary>
public interface IPasarelaPago
{
    string Nombre { get; }
    Task<TransaccionCreada> CrearTransaccionAsync(string ordenCompra, string idSesion, int monto, string urlRetorno);
    Task<ResultadoPago> ConfirmarTransaccionAsync(string token);
}

/// <summary>
/// Integración con Webpay Plus (Transbank) usando su API REST v1.2.
/// Documentación: https://www.transbankdevelopers.cl/referencia/webpay
/// Flujo: 1) crear transacción → token + url; 2) redirigir al usuario con POST token_ws;
///        3) Transbank vuelve a la url de retorno; 4) confirmar (commit) la transacción.
/// </summary>
public class WebpayPlusService : IPasarelaPago
{
    private const string RutaApi = "/rswebpaytransaction/api/webpay/v1.2/transactions";
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _http;
    private readonly ILogger<WebpayPlusService> _logger;

    public string Nombre => "Webpay";

    public WebpayPlusService(HttpClient http, IOptions<OpcionesPago> opciones, ILogger<WebpayPlusService> logger)
    {
        var o = opciones.Value;
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(o.WebpayUrlBase);
        _http.DefaultRequestHeaders.Add("Tbk-Api-Key-Id", o.CodigoComercio);
        _http.DefaultRequestHeaders.Add("Tbk-Api-Key-Secret", o.ApiKeySecret);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<TransaccionCreada> CrearTransaccionAsync(string ordenCompra, string idSesion, int monto, string urlRetorno)
    {
        var cuerpo = JsonSerializer.Serialize(new
        {
            buy_order = ordenCompra,
            session_id = idSesion,
            amount = monto,
            return_url = urlRetorno
        });

        using var respuesta = await _http.PostAsync(RutaApi, new StringContent(cuerpo, Encoding.UTF8, "application/json"));
        var texto = await respuesta.Content.ReadAsStringAsync();
        if (!respuesta.IsSuccessStatusCode)
        {
            _logger.LogError("Webpay create falló ({Status}): {Body}", (int)respuesta.StatusCode, texto);
            throw new InvalidOperationException("No se pudo iniciar el pago con Webpay.");
        }

        var datos = JsonSerializer.Deserialize<RespuestaCrear>(texto, Json)!;
        return new TransaccionCreada(datos.Token, datos.Url);
    }

    public async Task<ResultadoPago> ConfirmarTransaccionAsync(string token)
    {
        // El token viene del navegador: se valida su formato antes de usarlo en la URL
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100 || !token.All(char.IsLetterOrDigit))
            return new ResultadoPago { Aprobado = false, CodigoRespuesta = -1, Estado = ResultadoPago.ErrorComunicacion };

        // 1) Commit: PUT sin cuerpo a /transactions/{token}
        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Put, $"{RutaApi}/{Uri.EscapeDataString(token)}")
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            };
            using var respuesta = await _http.SendAsync(solicitud);
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (respuesta.IsSuccessStatusCode)
                return Mapear(JsonSerializer.Deserialize<RespuestaCommit>(texto, Json)!);

            // Por ejemplo, si el token ya fue confirmado (doble envío) o expiró: se consulta el estado real.
            _logger.LogWarning("Webpay commit falló ({Status}): {Body}", (int)respuesta.StatusCode, texto);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Sin comunicación con Webpay al confirmar; se consultará el estado");
        }

        // 2) Consulta de estado: GET /transactions/{token}. Así nunca se anula un pago que sí fue aprobado.
        return await ConsultarEstadoAsync(token);
    }

    private async Task<ResultadoPago> ConsultarEstadoAsync(string token)
    {
        try
        {
            using var respuesta = await _http.GetAsync($"{RutaApi}/{Uri.EscapeDataString(token)}");
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webpay status falló ({Status}): {Body}", (int)respuesta.StatusCode, texto);
                // 4xx: el token no existe o expiró sin pagarse → rechazo. 5xx: no se sabe → pendiente.
                return new ResultadoPago
                {
                    Aprobado = false, CodigoRespuesta = -1,
                    Estado = (int)respuesta.StatusCode >= 500 ? ResultadoPago.ErrorComunicacion : "ERROR"
                };
            }
            var d = JsonSerializer.Deserialize<RespuestaCommit>(texto, Json)!;
            // INITIALIZED = el cliente aún no termina de pagar: no se toca el pedido
            if (d.Status == "INITIALIZED")
                return new ResultadoPago { Aprobado = false, CodigoRespuesta = -1, Estado = ResultadoPago.ErrorComunicacion };
            return Mapear(d);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Sin comunicación con Webpay al consultar el estado");
            return new ResultadoPago { Aprobado = false, CodigoRespuesta = -1, Estado = ResultadoPago.ErrorComunicacion };
        }
    }

    private static ResultadoPago Mapear(RespuestaCommit d) => new()
    {
        Aprobado = d.ResponseCode == 0 && d.Status == "AUTHORIZED",
        Monto = (int)d.Amount,
        OrdenCompra = d.BuyOrder,
        CodigoAutorizacion = d.AuthorizationCode,
        UltimosDigitosTarjeta = d.CardDetail?.CardNumber,
        TipoPago = d.PaymentTypeCode,
        Cuotas = d.InstallmentsNumber,
        CodigoRespuesta = d.ResponseCode ?? -1,
        Estado = d.Status
    };

    private class RespuestaCrear
    {
        [JsonPropertyName("token")] public string Token { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    private class RespuestaCommit
    {
        [JsonPropertyName("vci")] public string? Vci { get; set; }
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("buy_order")] public string? BuyOrder { get; set; }
        [JsonPropertyName("session_id")] public string? SessionId { get; set; }
        [JsonPropertyName("card_detail")] public DetalleTarjeta? CardDetail { get; set; }
        [JsonPropertyName("authorization_code")] public string? AuthorizationCode { get; set; }
        [JsonPropertyName("payment_type_code")] public string? PaymentTypeCode { get; set; }
        [JsonPropertyName("response_code")] public int? ResponseCode { get; set; }
        [JsonPropertyName("installments_number")] public int? InstallmentsNumber { get; set; }
    }

    private class DetalleTarjeta
    {
        [JsonPropertyName("card_number")] public string? CardNumber { get; set; }
    }
}

/// <summary>
/// Pasarela falsa para presentar el proyecto sin conexión a internet.
/// Redirige a una página local (/Pago/Simulador) que imita el formulario de pago.
/// </summary>
public class PagoSimuladoService : IPasarelaPago
{
    // Guarda en memoria los tokens emitidos y si fueron aprobados en el simulador.
    private static readonly Dictionary<string, (string Orden, int Monto, bool? Aprobado)> Tokens = new();
    private static readonly object Candado = new();

    public string Nombre => "Simulado";

    public Task<TransaccionCreada> CrearTransaccionAsync(string ordenCompra, string idSesion, int monto, string urlRetorno)
    {
        var token = "SIM" + Guid.NewGuid().ToString("N");
        lock (Candado) Tokens[token] = (ordenCompra, monto, null);
        return Task.FromResult(new TransaccionCreada(token, "/Pago/Simulador"));
    }

    public static bool RegistrarDecision(string token, bool aprobado)
    {
        lock (Candado)
        {
            if (!Tokens.TryGetValue(token, out var t)) return false;
            Tokens[token] = (t.Orden, t.Monto, aprobado);
            return true;
        }
    }

    public static (string Orden, int Monto)? ObtenerDatos(string token)
    {
        lock (Candado)
            return Tokens.TryGetValue(token, out var t) ? (t.Orden, t.Monto) : null;
    }

    public Task<ResultadoPago> ConfirmarTransaccionAsync(string token)
    {
        (string Orden, int Monto, bool? Aprobado) t;
        lock (Candado)
        {
            // Token desconocido o ya usado (doble envío): no se sabe nada nuevo, el pedido no cambia
            if (!Tokens.TryGetValue(token, out t))
                return Task.FromResult(new ResultadoPago { Aprobado = false, CodigoRespuesta = -1, Estado = ResultadoPago.ErrorComunicacion });
            if (t.Aprobado is null)
                return Task.FromResult(new ResultadoPago { Aprobado = false, CodigoRespuesta = -1, Estado = ResultadoPago.ErrorComunicacion });
            Tokens.Remove(token); // un token solo se puede confirmar una vez, igual que en Webpay
        }

        var aprobado = t.Aprobado == true;
        return Task.FromResult(new ResultadoPago
        {
            Aprobado = aprobado,
            Monto = t.Monto,
            OrdenCompra = t.Orden,
            CodigoAutorizacion = aprobado ? Random.Shared.Next(100000, 999999).ToString() : null,
            UltimosDigitosTarjeta = "6623",
            TipoPago = "VD",
            Cuotas = 0,
            CodigoRespuesta = aprobado ? 0 : -1,
            Estado = aprobado ? "AUTHORIZED" : "FAILED"
        });
    }
}
