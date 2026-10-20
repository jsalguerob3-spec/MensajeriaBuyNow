using System.Net.Mail;
using CapaEntidades;

namespace CapaLogica;

public class GestorCompras
{
    private readonly IRepositorio repositorio;
    private readonly IServicioCorreo correo;
    private readonly int limiteDiario;

    /// <summary>
    /// Recibe el repositorio, el servicio de correo y el límite diario de intentos.
    /// </summary>
    public GestorCompras(IRepositorio almacenamiento, IServicioCorreo servicioCorreo, int limite)
    {
        repositorio = almacenamiento;
        correo = servicioCorreo;
        limiteDiario = Math.Clamp(limite, 1, 100);
    }

    /// <summary>
    /// Obtiene las compras y las notificaciones guardadas.
    /// </summary>
    public RegistroDatos Consultar()
    {
        return repositorio.Cargar();
    }

    /// <summary>
    /// Valida los datos del cliente y guarda una nueva compra con su estado inicial.
    /// </summary>
    public Compra Registrar(string cliente, string direccion, string producto, decimal total)
    {
        cliente = cliente.Trim();
        direccion = direccion.Trim();
        producto = producto.Trim();
        if (cliente.Length == 0 || producto.Length == 0)
        {
            throw new InvalidOperationException("El cliente y el producto son obligatorios.");
        }
        if (!MailAddress.TryCreate(direccion, out MailAddress? validada) || validada.Address != direccion)
        {
            throw new InvalidOperationException("Ingresá un correo válido, sin nombres ni espacios adicionales.");
        }
        if (total <= 0)
        {
            throw new InvalidOperationException("El total debe ser mayor a cero.");
        }

        RegistroDatos datos = repositorio.Cargar();
        int ultimoId = 0;
        foreach (Compra existente in datos.Compras)
        {
            if (existente.Id > ultimoId)
            {
                ultimoId = existente.Id;
            }
        }
        Compra compra = new Compra();
        compra.Id = ultimoId + 1;
        compra.Cliente = cliente;
        compra.Correo = direccion;
        compra.Producto = producto;
        compra.Total = total;
        CambioEstado cambio = new CambioEstado();
        cambio.Estado = compra.Estado;
        compra.Cambios.Add(cambio);
        datos.Compras.Add(compra);
        repositorio.Guardar(datos);
        return compra;
    }

    /// <summary>
    /// Obtiene una compra a partir de su número.
    /// </summary>
    public Compra Buscar(int id)
    {
        return BuscarEnDatos(repositorio.Cargar(), id);
    }

    /// <summary>
    /// Busca la compra en los datos cargados e informa si no existe.
    /// </summary>
    private Compra BuscarEnDatos(RegistroDatos datos, int id)
    {
        foreach (Compra compra in datos.Compras)
        {
            if (compra.Id == id)
            {
                return compra;
            }
        }
        throw new InvalidOperationException("No existe una compra con ese número.");
    }

    /// <summary>
    /// Valida el cambio de estado y lo agrega al historial de la compra.
    /// </summary>
    public void CambiarEstado(int id, EstadoCompra nuevo)
    {
        RegistroDatos datos = repositorio.Cargar();
        Compra compra = BuscarEnDatos(datos, id);
        bool permitido = false;
        if (compra.Estado == EstadoCompra.Recibida)
        {
            permitido = nuevo == EstadoCompra.Preparando || nuevo == EstadoCompra.Cancelada;
        }
        else if (compra.Estado == EstadoCompra.Preparando)
        {
            permitido = nuevo == EstadoCompra.Enviada || nuevo == EstadoCompra.Cancelada;
        }
        else if (compra.Estado == EstadoCompra.Enviada)
        {
            permitido = nuevo == EstadoCompra.Entregada;
        }
        if (!permitido)
        {
            throw new InvalidOperationException("Ese cambio no corresponde al progreso de la compra.");
        }
        compra.Estado = nuevo;
        CambioEstado cambio = new CambioEstado();
        cambio.Estado = nuevo;
        compra.Cambios.Add(cambio);
        repositorio.Guardar(datos);
    }

    /// <summary>
    /// Arma el texto del correo con los datos y el estado actual de la compra.
    /// </summary>
    public string PrepararMensaje(int id)
    {
        Compra compra = Buscar(id);
        return "Hola, " + compra.Cliente + ".\n\n"
            + "Tu compra #" + compra.Id + " en BuyNow está en estado: " + compra.Estado + ".\n"
            + "Producto o descripción: " + compra.Producto + "\n"
            + "Total: Q " + compra.Total.ToString("F2") + "\n\n"
            + "Gracias por comprar en BuyNow.";
    }

    /// <summary>
    /// Controla la cuota y los duplicados, guarda el intento antes del envío y registra su resultado.
    /// </summary>
    public async Task<Notificacion> NotificarAsync(int id)
    {
        RegistroDatos datos = repositorio.Cargar();
        Compra compra = BuscarEnDatos(datos, id);
        int intentosHoy = 0;
        foreach (Notificacion anterior in datos.Notificaciones)
        {
            if (anterior.Fecha.UtcDateTime.Date == DateTime.UtcNow.Date)
            {
                intentosHoy++;
            }
            if (anterior.CompraId == id && anterior.EstadoCompra == compra.Estado
                && (anterior.Resultado == "Aceptado" || anterior.Resultado == "En proceso" || anterior.Resultado == "Incierto"))
            {
                throw new InvalidOperationException("Este estado ya se notificó o tiene un envío incierto. Revisá el historial y Brevo.");
            }
        }
        if (intentosHoy >= limiteDiario)
        {
            throw new InvalidOperationException("Se alcanzó el límite local diario de intentos de correo.");
        }

        Notificacion notificacion = new Notificacion();
        notificacion.CompraId = id;
        notificacion.EstadoCompra = compra.Estado;
        notificacion.Destinatario = compra.Correo;
        datos.Notificaciones.Add(notificacion);
        repositorio.Guardar(datos);
        try
        {
            notificacion.Detalle = await correo.EnviarAsync(compra.Correo, compra.Cliente,
                "BuyNow | Compra #" + compra.Id + " - " + compra.Estado, PrepararMensaje(id));
            notificacion.Resultado = "Aceptado";
        }
        catch (InvalidOperationException error)
        {
            notificacion.Resultado = "Rechazado";
            notificacion.Detalle = error.Message;
        }
        catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is System.Text.Json.JsonException || error is KeyNotFoundException)
        {
            notificacion.Resultado = "Incierto";
            notificacion.Detalle = "No se pudo confirmar la respuesta. Revisá Brevo antes de volver a enviar.";
        }
        repositorio.Guardar(datos);
        return notificacion;
    }
}
