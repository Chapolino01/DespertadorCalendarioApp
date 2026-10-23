using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DespertadorApp.Services
{
    public static class TemaService
    {
        private static readonly string Carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DespertadorApp");

        private static readonly string ArchivoTransparencia = Path.Combine(Carpeta, "transparencia.txt");
        private static readonly string[] Extensiones = { ".png", ".jpg", ".jpeg", ".bmp" };

        public static string? RutaFondo =>
            Extensiones.Select(e => Path.Combine(Carpeta, "fondo" + e)).FirstOrDefault(File.Exists);

        // Porcentaje de transparencia de los cuadros cuando hay imagen de fondo (0 a 80)
        public static int Transparencia
        {
            get
            {
                try
                {
                    if (File.Exists(ArchivoTransparencia)
                        && int.TryParse(File.ReadAllText(ArchivoTransparencia).Trim(), out var valor))
                        return Math.Clamp(valor, 0, 80);
                }
                catch { }

                return 45;
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(Carpeta);
                    File.WriteAllText(ArchivoTransparencia, Math.Clamp(value, 0, 80).ToString());
                }
                catch { }
            }
        }

        // Copia la imagen a la carpeta de la app. Devuelve false si no se pudo usar.
        public static bool ElegirFondo(string origen)
        {
            try
            {
                var extension = Path.GetExtension(origen).ToLowerInvariant();
                if (!Extensiones.Contains(extension)) return false;

                // Verifica que se pueda leer como imagen antes de reemplazar la actual
                var prueba = new BitmapImage();
                prueba.BeginInit();
                prueba.CacheOption = BitmapCacheOption.OnLoad;
                prueba.UriSource = new Uri(origen);
                prueba.EndInit();

                Directory.CreateDirectory(Carpeta);
                QuitarArchivos();
                File.Copy(origen, Path.Combine(Carpeta, "fondo" + extension), true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void QuitarFondo() => QuitarArchivos();

        private static void QuitarArchivos()
        {
            foreach (var extension in Extensiones)
            {
                try
                {
                    var ruta = Path.Combine(Carpeta, "fondo" + extension);
                    if (File.Exists(ruta)) File.Delete(ruta);
                }
                catch { }
            }
        }

        // Actualiza el fondo y los colores de los cuadros en toda la app
        public static void Aplicar()
        {
            var recursos = Application.Current.Resources;
            var imagen = CargarImagen();

            if (imagen != null)
            {
                var opacidad = 1 - Transparencia / 100.0;
                var alfa = (byte)Math.Round(opacidad * 255);

                recursos["FondoVentana"] = imagen;
                recursos["Tarjeta"] = new SolidColorBrush(Color.FromArgb(alfa, 0x1B, 0x27, 0x40));
                recursos["TarjetaInterna"] = new SolidColorBrush(Color.FromArgb(alfa, 0x16, 0x21, 0x3A));
                recursos["TarjetaFrase"] = GradienteFrase(alfa);
            }
            else
            {
                recursos["FondoVentana"] = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0x0F, 0x1B, 0x33), 0),
                        new GradientStop(Color.FromRgb(0x0A, 0x10, 0x20), 1)
                    },
                    new Point(0, 0), new Point(1, 1));
                recursos["Tarjeta"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x27, 0x40));
                recursos["TarjetaInterna"] = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3A));
                recursos["TarjetaFrase"] = GradienteFrase(255);
            }
        }

        private static Brush? CargarImagen()
        {
            var ruta = RutaFondo;
            if (ruta == null) return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // no deja el archivo bloqueado
                bitmap.DecodePixelWidth = 1920;                // limita el uso de memoria
                bitmap.UriSource = new Uri(ruta);
                bitmap.EndInit();
                bitmap.Freeze();

                return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }
            catch
            {
                return null;
            }
        }

        private static Brush GradienteFrase(byte alfa)
        {
            return new RadialGradientBrush
            {
                Center = new Point(0.8, 1),
                RadiusX = 0.9,
                RadiusY = 0.9,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(alfa, 0x2A, 0x5F, 0x66), 0),
                    new GradientStop(Color.FromArgb(alfa, 0x1B, 0x27, 0x40), 1)
                }
            };
        }
    }
}