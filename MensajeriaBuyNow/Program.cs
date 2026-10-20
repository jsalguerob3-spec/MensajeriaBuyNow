using System.Text;
using System.Text.Json;
using CapaDatos;
using CapaEntidades;
using CapaLogica;
using CapaServicios;

namespace CapaPresentacion;

internal class Program
{
    /// <summary>
    /// Prepara las capas, carga la configuración y abre el menú de la aplicación.
    /// </summary>
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        string carpeta = Path.Combine(AppContext.BaseDirectory, "Datos");
        try
        {
            Directory.CreateDirectory(carpeta);
            using FileStream bloqueo = new FileStream(Path.Combine(carpeta, "aplicacion.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            string ruta = Path.Combine(AppContext.BaseDirectory, "correo.local.json");
            ConfiguracionCorreo ajustes = new ConfiguracionCorreo();
            if (File.Exists(ruta))
            {
                ajustes = JsonSerializer.Deserialize<ConfiguracionCorreo>(File.ReadAllText(ruta))
                    ?? throw new InvalidDataException("La configuración no es válida.");
            }
            using HttpClient cliente = new HttpClient();
            cliente.Timeout = TimeSpan.FromSeconds(30);
            RepositorioJson repositorio = new RepositorioJson(Path.Combine(carpeta, "compras.json"));
            ServicioCorreoBrevo correo = new ServicioCorreoBrevo(cliente, ajustes);
            GestorCompras gestor = new GestorCompras(repositorio, correo, ajustes.LimiteDiario);
            MenuPrincipal menu = new MenuPrincipal(gestor);
            await menu.EjecutarAsync();
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
        {
            Console.WriteLine("No se pudo abrir o guardar el proyecto: " + error.Message);
            Console.WriteLine("Revisá los archivos y que no haya otra instancia abierta.");
            Environment.ExitCode = 1;
        }
    }
}
