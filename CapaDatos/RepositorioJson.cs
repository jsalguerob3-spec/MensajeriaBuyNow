using System.Text.Json;
using CapaEntidades;

namespace CapaDatos;

public class RepositorioJson : IRepositorio
{
    private readonly string ruta;
    private readonly JsonSerializerOptions opciones;

    /// <summary>
    /// Define la ruta del archivo y el formato del JSON.
    /// </summary>
    public RepositorioJson(string rutaArchivo)
    {
        ruta = rutaArchivo;
        opciones = new JsonSerializerOptions();
        opciones.WriteIndented = true;
    }

    /// <summary>
    /// Lee los datos guardados o devuelve un registro vacío si el archivo no existe.
    /// </summary>
    public RegistroDatos Cargar()
    {
        if (!File.Exists(ruta))
        {
            return new RegistroDatos();
        }

        string contenido = File.ReadAllText(ruta);
        RegistroDatos? datos = JsonSerializer.Deserialize<RegistroDatos>(contenido);
        if (datos == null || datos.Compras == null || datos.Notificaciones == null)
        {
            throw new InvalidDataException("El archivo de datos no es válido. Revisalo antes de continuar.");
        }
        return datos;
    }

    /// <summary>
    /// Escribe los datos en un archivo temporal y luego reemplaza el archivo principal.
    /// </summary>
    public void Guardar(RegistroDatos datos)
    {
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }
        string temporal = ruta + ".tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(datos, opciones));
        File.Move(temporal, ruta, true);
    }
}
