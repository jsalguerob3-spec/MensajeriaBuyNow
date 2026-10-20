using System.Globalization;
using CapaEntidades;
using CapaLogica;

namespace CapaPresentacion;

public class MenuPrincipal
{
    private readonly GestorCompras gestor;

    /// <summary>
    /// Recibe el gestor que se usará para trabajar con las compras.
    /// </summary>
    public MenuPrincipal(GestorCompras gestorCompras)
    {
        gestor = gestorCompras;
    }

    /// <summary>
    /// Muestra el menú y atiende las opciones hasta que el usuario decide salir.
    /// </summary>
    public async Task EjecutarAsync()
    {
        bool continuar = true;
        while (continuar)
        {
            LimpiarPantalla();
            Console.WriteLine("\n==========================================");
            Console.WriteLine("       BUY NOW | Seguimiento de compras");
            Console.WriteLine("==========================================");
            Console.WriteLine("1. Registrar compra");
            Console.WriteLine("2. Listar compras");
            Console.WriteLine("3. Consultar compra y sus cambios");
            Console.WriteLine("4. Cambiar estado");
            Console.WriteLine("5. Notificar estado por correo");
            Console.WriteLine("6. Historial de correos");
            Console.WriteLine("0. Salir");
            Console.Write("Elegí una opción: ");
            string? opcion = Console.ReadLine();
            LimpiarPantalla();
            try
            {
                switch (opcion)
                {
                    case "1":
                        Registrar();
                        break;
                    case "2":
                        Listar();
                        break;
                    case "3":
                        Consultar();
                        break;
                    case "4":
                        CambiarEstado();
                        break;
                    case "5":
                        await NotificarAsync();
                        break;
                    case "6":
                        MostrarHistorial();
                        break;
                    case "0":
                    case null:
                        continuar = false;
                        break;
                    default:
                        Console.WriteLine("Elegí una opción del menú.");
                        break;
                }
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("Aviso: " + error.Message);
            }
            catch (EndOfStreamException)
            {
                continuar = false;
            }
            if (continuar)
            {
                Console.Write("\nPresioná Enter para volver al menú...");
                if (Console.ReadLine() == null)
                {
                    continuar = false;
                }
            }
        }
        Console.WriteLine("Hasta luego. Tus compras quedaron guardadas.");
    }

    /// <summary>
    /// Limpia la consola cuando la salida se muestra en una terminal.
    /// </summary>
    private void LimpiarPantalla()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }
    }

    /// <summary>
    /// Solicita un texto y quita los espacios del inicio y del final.
    /// </summary>
    private string Leer(string etiqueta)
    {
        Console.Write(etiqueta);
        string? valor = Console.ReadLine();
        if (valor == null)
        {
            throw new EndOfStreamException();
        }
        return valor.Trim();
    }

    /// <summary>
    /// Solicita un número entero positivo y valida lo ingresado.
    /// </summary>
    private int LeerNumero(string etiqueta)
    {
        if (!int.TryParse(Leer(etiqueta), out int numero) || numero <= 0)
        {
            throw new InvalidOperationException("Ingresá un número entero mayor a cero.");
        }
        return numero;
    }

    /// <summary>
    /// Pide los datos de la compra y la registra mediante la capa lógica.
    /// </summary>
    private void Registrar()
    {
        Console.WriteLine("\nNUEVA COMPRA");
        string cliente = Leer("Nombre del cliente: ");
        string correo = Leer("Correo electrónico: ");
        string producto = Leer("Producto o descripción: ");
        string texto = Leer("Total en quetzales (ejemplo 150.50, sin separador de miles): ");
        if (!decimal.TryParse(texto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal total))
        {
            throw new InvalidOperationException("Ingresá el total usando punto para los decimales.");
        }
        Compra compra = gestor.Registrar(cliente, correo, producto, total);
        Console.WriteLine("Compra #" + compra.Id + " registrada. Estado: " + compra.Estado);
        Console.WriteLine("Podés enviar su correo desde la opción 5.");
    }

    /// <summary>
    /// Muestra un resumen de las compras guardadas.
    /// </summary>
    private void Listar()
    {
        RegistroDatos datos = gestor.Consultar();
        Console.WriteLine("\nCOMPRAS REGISTRADAS: " + datos.Compras.Count);
        foreach (Compra compra in datos.Compras)
        {
            Console.WriteLine("#" + compra.Id + " | " + compra.Cliente + " | " + compra.Producto
                + " | Q " + compra.Total.ToString("F2") + " | " + compra.Estado);
        }
    }

    /// <summary>
    /// Muestra los datos y los cambios de estado de una compra.
    /// </summary>
    private void Consultar()
    {
        Compra compra = gestor.Buscar(LeerNumero("Número de compra: "));
        Console.WriteLine(gestor.PrepararMensaje(compra.Id));
        Console.WriteLine("Correo: " + compra.Correo);
        foreach (CambioEstado cambio in compra.Cambios)
        {
            Console.WriteLine("  " + cambio.Fecha.ToString("g") + " | " + cambio.Estado);
        }
    }

    /// <summary>
    /// Solicita el nuevo estado y guarda el cambio si está permitido.
    /// </summary>
    private void CambiarEstado()
    {
        int id = LeerNumero("Número de compra: ");
        Console.WriteLine("Estado actual: " + gestor.Buscar(id).Estado);
        Console.WriteLine("2. Preparando | 3. Enviada | 4. Entregada | 5. Cancelada");
        int estado = LeerNumero("Nuevo estado: ");
        gestor.CambiarEstado(id, (EstadoCompra)estado);
        Console.WriteLine("Estado actualizado. Usá la opción 5 para notificar al cliente.");
    }

    /// <summary>
    /// Muestra el correo y solicita confirmación antes de enviarlo.
    /// </summary>
    private async Task NotificarAsync()
    {
        int id = LeerNumero("Número de compra: ");
        Compra compra = gestor.Buscar(id);
        Console.WriteLine("\nPara: " + compra.Correo);
        Console.WriteLine(gestor.PrepararMensaje(id));
        string respuesta = Leer("\n¿Enviar este correo real? Escribí SI para confirmar: ");
        if (!respuesta.Equals("SI", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Envío cancelado.");
            return;
        }
        Console.WriteLine("Enviando correo...");
        Notificacion resultado = await gestor.NotificarAsync(id);
        if (resultado.Resultado == "Aceptado")
        {
            Console.WriteLine("Correo enviado");
        }
        else if (resultado.Resultado == "Incierto")
        {
            Console.WriteLine("No se pudo confirmar el envío. Revisá el historial.");
        }
        else
        {
            Console.WriteLine("No se pudo enviar el correo. Revisá el historial.");
        }
    }

    /// <summary>
    /// Muestra los intentos de correo y el resultado de cada uno.
    /// </summary>
    private void MostrarHistorial()
    {
        RegistroDatos datos = gestor.Consultar();
        Console.WriteLine("\nHISTORIAL DE CORREOS: " + datos.Notificaciones.Count);
        foreach (Notificacion aviso in datos.Notificaciones)
        {
            Console.WriteLine(aviso.Fecha.ToString("g") + " | Compra #" + aviso.CompraId
                + " | " + aviso.EstadoCompra + " | " + aviso.Destinatario + " | " + aviso.Resultado);
            Console.WriteLine("  " + aviso.Detalle);
        }
    }
}
