using System.Net.Http.Json;
using System.Text.Json;
using CapaEntidades;

namespace CapaServicios;

public class ServicioCorreoBrevo : IServicioCorreo
{
    private readonly HttpClient cliente;
    private readonly ConfiguracionCorreo configuracion;

    /// <summary>
    /// Recibe el cliente HTTP y la configuración de la cuenta de correo.
    /// </summary>
    public ServicioCorreoBrevo(HttpClient clienteHttp, ConfiguracionCorreo ajustes)
    {
        cliente = clienteHttp;
        configuracion = ajustes;
    }

    /// <summary>
    /// Envía el correo a Brevo y devuelve el identificador del mensaje aceptado.
    /// </summary>
    public async Task<string> EnviarAsync(string destinatario, string nombre, string asunto, string contenido)
    {
        if (string.IsNullOrWhiteSpace(configuracion.ApiKey))
        {
            throw new InvalidOperationException("Falta la clave de Brevo en correo.local.json.");
        }

        Dictionary<string, string> remitente = new Dictionary<string, string>();
        remitente.Add("name", configuracion.Nombre);
        remitente.Add("email", configuracion.Remitente);
        Dictionary<string, string> receptor = new Dictionary<string, string>();
        receptor.Add("name", nombre);
        receptor.Add("email", destinatario);
        List<Dictionary<string, string>> destinatarios = new List<Dictionary<string, string>>();
        destinatarios.Add(receptor);
        Dictionary<string, object> mensaje = new Dictionary<string, object>();
        mensaje.Add("sender", remitente);
        mensaje.Add("to", destinatarios);
        mensaje.Add("subject", asunto);
        mensaje.Add("textContent", contenido);

        using HttpRequestMessage solicitud = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        solicitud.Headers.Add("api-key", configuracion.ApiKey);
        solicitud.Content = JsonContent.Create(mensaje);
        using HttpResponseMessage respuesta = await cliente.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Brevo rechazó el envío (HTTP " + (int)respuesta.StatusCode
                + "). Revisá la clave, el remitente y la cuota en Brevo.");
        }

        string contenidoRespuesta = await respuesta.Content.ReadAsStringAsync();
        using JsonDocument documento = JsonDocument.Parse(contenidoRespuesta);
        string? identificador = documento.RootElement.GetProperty("messageId").GetString();
        if (string.IsNullOrWhiteSpace(identificador))
        {
            throw new JsonException("Brevo no devolvió el identificador. Revisá su panel antes de reenviar.");
        }
        return identificador;
    }
}
