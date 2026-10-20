using System.Net;
using System.Text.Json;
using CapaDatos;
using CapaEntidades;
using CapaLogica;
using CapaServicios;

namespace Pruebas;

internal class Program
{
    /// <summary>
    /// Comprueba una condición e informa si la prueba pasó o falló.
    /// </summary>
    private static void Verificar(bool condicion, string nombre)
    {
        if (!condicion)
        {
            throw new Exception("FALLÓ: " + nombre);
        }
        Console.WriteLine("OK: " + nombre);
    }

    /// <summary>
    /// Ejecuta las pruebas de compras, persistencia y correos sin realizar envíos reales.
    /// </summary>
    private static async Task Main()
    {
        string carpeta = Path.Combine(AppContext.BaseDirectory, "DatosPruebas", Guid.NewGuid().ToString());
        Directory.CreateDirectory(carpeta);
        RepositorioJson repositorio = new RepositorioJson(Path.Combine(carpeta, "datos.json"));
        CorreoPrueba correo = new CorreoPrueba();
        GestorCompras gestor = new GestorCompras(repositorio, correo, 100);
        Compra compra = gestor.Registrar("Ana", "ana@example.com", "Teclado", 150.50m);
        GestorCompras reabierto = new GestorCompras(repositorio, correo, 100);
        Verificar(reabierto.Buscar(compra.Id).Total == 150.50m, "Persistencia al reabrir");
        bool rechazo = false;
        try
        {
            gestor.Registrar("Ana", "incorrecto", "Teclado", 10);
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo && gestor.Consultar().Compras.Count == 1, "Correo inválido no crea compra");
        rechazo = false;
        try
        {
            gestor.CambiarEstado(compra.Id, EstadoCompra.Entregada);
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo, "Impide saltar estados");
        Notificacion resultado = await gestor.NotificarAsync(compra.Id);
        Verificar(resultado.Resultado == "Aceptado", "Registra aceptación del proveedor");
        rechazo = false;
        try
        {
            await gestor.NotificarAsync(compra.Id);
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo && correo.Intentos == 1, "Evita duplicados");
        gestor.CambiarEstado(compra.Id, EstadoCompra.Preparando);
        correo.Fallar = true;
        resultado = await gestor.NotificarAsync(compra.Id);
        Verificar(resultado.Resultado == "Incierto", "Timeout conserva intento incierto");
        rechazo = false;
        try
        {
            await gestor.NotificarAsync(compra.Id);
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo && correo.Intentos == 2, "No reintenta envío incierto");
        gestor.CambiarEstado(compra.Id, EstadoCompra.Enviada);
        gestor.CambiarEstado(compra.Id, EstadoCompra.Entregada);
        Verificar(gestor.Buscar(compra.Id).Cambios.Count == 4, "Conserva historial de estados");
        Compra segunda = gestor.Registrar("Luis", "luis@example.com", "Monitor", 900);
        gestor.CambiarEstado(segunda.Id, EstadoCompra.Cancelada);
        Verificar(gestor.Buscar(segunda.Id).Estado == EstadoCompra.Cancelada, "Cancelación antes del envío");
        GestorCompras limitado = new GestorCompras(repositorio, correo, 2);
        rechazo = false;
        try
        {
            await limitado.NotificarAsync(segunda.Id);
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo && correo.Intentos == 2, "Cuota persiste y bloquea antes de contactar proveedor");

        HttpPrueba transporte = new HttpPrueba();
        using HttpClient cliente = new HttpClient(transporte);
        ConfiguracionCorreo ajustes = new ConfiguracionCorreo();
        ajustes.ApiKey = "clave-ficticia";
        ServicioCorreoBrevo servicio = new ServicioCorreoBrevo(cliente, ajustes);
        string identificador = await servicio.EnviarAsync("ana@example.com", "Ana", "Compra", "Hola");
        Verificar(identificador == "mensaje-prueba" && transporte.SolicitudCorrecta, "Contrato HTTP de Brevo");
        transporte.Codigo = HttpStatusCode.Unauthorized;
        rechazo = false;
        try
        {
            await servicio.EnviarAsync("ana@example.com", "Ana", "Compra", "Hola");
        }
        catch (InvalidOperationException)
        {
            rechazo = true;
        }
        Verificar(rechazo, "HTTP 401 se informa como rechazo");
        File.WriteAllText(Path.Combine(carpeta, "datos.json"), "{roto");
        bool corrupto = false;
        try
        {
            repositorio.Cargar();
        }
        catch (JsonException)
        {
            corrupto = true;
        }
        Verificar(corrupto, "Archivo corrupto no se reemplaza por datos vacíos");
        Console.WriteLine("Pruebas completadas sin enviar correos reales.");
    }
}

internal class CorreoPrueba : IServicioCorreo
{
    public int Intentos { get; private set; }
    public bool Fallar { get; set; }

    /// <summary>
    /// Simula un correo aceptado o una interrupción para probar la lógica.
    /// </summary>
    public Task<string> EnviarAsync(string destinatario, string nombre, string asunto, string contenido)
    {
        Intentos++;
        if (Fallar)
        {
            throw new TaskCanceledException();
        }
        return Task.FromResult("mensaje-prueba");
    }
}

internal class HttpPrueba : HttpMessageHandler
{
    public bool SolicitudCorrecta { get; private set; }
    public HttpStatusCode Codigo { get; set; } = HttpStatusCode.Created;

    /// <summary>
    /// Revisa la solicitud HTTP y devuelve una respuesta simulada de Brevo.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
    {
        string cuerpo = await solicitud.Content!.ReadAsStringAsync(cancelacion);
        using JsonDocument documento = JsonDocument.Parse(cuerpo);
        SolicitudCorrecta = solicitud.Method == HttpMethod.Post
            && solicitud.RequestUri!.ToString() == "https://api.brevo.com/v3/smtp/email"
            && solicitud.Headers.Contains("api-key")
            && documento.RootElement.GetProperty("sender").GetProperty("email").GetString() == "no-reply@buynow.org.uk"
            && documento.RootElement.GetProperty("to")[0].GetProperty("email").GetString() == "ana@example.com"
            && documento.RootElement.GetProperty("textContent").GetString() == "Hola";
        HttpResponseMessage respuesta = new HttpResponseMessage(Codigo);
        respuesta.Content = new StringContent("{\"messageId\":\"mensaje-prueba\"}");
        return respuesta;
    }
}
