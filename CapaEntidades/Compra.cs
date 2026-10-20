namespace CapaEntidades;

public enum EstadoCompra
{
    Recibida = 1,
    Preparando = 2,
    Enviada = 3,
    Entregada = 4,
    Cancelada = 5
}

public class Compra
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Producto { get; set; } = "";
    public decimal Total { get; set; }
    public EstadoCompra Estado { get; set; } = EstadoCompra.Recibida;
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public List<CambioEstado> Cambios { get; set; } = new List<CambioEstado>();
}

public class CambioEstado
{
    public EstadoCompra Estado { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
}

public class Notificacion
{
    public int CompraId { get; set; }
    public EstadoCompra EstadoCompra { get; set; }
    public string Destinatario { get; set; } = "";
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public string Resultado { get; set; } = "En proceso";
    public string Detalle { get; set; } = "";
}

public class RegistroDatos
{
    public List<Compra> Compras { get; set; } = new List<Compra>();
    public List<Notificacion> Notificaciones { get; set; } = new List<Notificacion>();
}

public class ConfiguracionCorreo
{
    public string ApiKey { get; set; } = "";
    public string Remitente { get; set; } = "no-reply@buynow.org.uk";
    public string Nombre { get; set; } = "BuyNow";
    public int LimiteDiario { get; set; } = 100;
}

public interface IRepositorio
{
    /// <summary>
    /// Define la lectura de las compras y las notificaciones guardadas.
    /// </summary>
    RegistroDatos Cargar();
    /// <summary>
    /// Define el guardado de las compras y las notificaciones.
    /// </summary>
    void Guardar(RegistroDatos datos);
}

public interface IServicioCorreo
{
    /// <summary>
    /// Define el envío de un correo y la devolución de su identificador.
    /// </summary>
    Task<string> EnviarAsync(string destinatario, string nombre, string asunto, string contenido);
}
