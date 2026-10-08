using Microsoft.AspNetCore.RateLimiting;
using Ferreteria.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers;

/// <summary>API del chatbot "Maestro Bot" que usa el widget flotante (wwwroot/js/chatbot.js).</summary>
public class ChatbotController : Controller
{
    private readonly ChatbotService _bot;

    public ChatbotController(ChatbotService bot) => _bot = bot;

    public record MensajeChat(string? Texto);

    // POST /Chatbot/Responder  { "texto": "¿cuánto cuesta el despacho?" }
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(Seguridad.PoliticaChatbot)]
    public async Task<IActionResult> Responder([FromBody] MensajeChat mensaje) =>
        Json(await _bot.ResponderAsync(mensaje?.Texto));
}
