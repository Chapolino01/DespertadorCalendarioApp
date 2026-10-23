using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DespertadorApp.Data;
using DespertadorApp.Models;
using DespertadorApp.Services;
using Microsoft.Win32;

namespace DespertadorApp
{
    public partial class ConfiguracionWindow : Window
    {
        private readonly AppDbContext _db = new();
        private readonly PlaylistService _playlistService = new();
        private readonly FrasesService _frasesService = new();

        public ConfiguracionWindow()
        {
            InitializeComponent();
            CargarPlaylists();
            txtFuente.Text = _db.Fuentes.FirstOrDefault()?.Url ?? "";

            sldTransparencia.Value = TemaService.Transparencia;
            ActualizarEtiquetaTransparencia();
            ActualizarEstadoFondo();
        }

        private static bool UrlValida(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        // ---------- Playlists ----------

        private void CargarPlaylists()
        {
            lstPlaylists.ItemsSource = null;
            lstPlaylists.ItemsSource = _db.Playlists.ToList();
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = txtNombre.Text.Trim();
            var url = txtUrl.Text.Trim();

            if (nombre == "" || !UrlValida(url))
            {
                MessageBox.Show("Ingresá un nombre y una URL válida (http o https).");
                return;
            }

            _db.Playlists.Add(new PlaylistUrl { Nombre = nombre, Url = url });
            _db.SaveChanges();

            txtNombre.Clear();
            txtUrl.Clear();
            CargarPlaylists();
        }

        private void Eliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstPlaylists.SelectedItem is not PlaylistUrl seleccionada)
            {
                MessageBox.Show("Seleccioná una playlist.");
                return;
            }

            _db.Playlists.Remove(seleccionada);
            _db.SaveChanges();
            CargarPlaylists();
        }

        private async void Probar_Click(object sender, RoutedEventArgs e)
        {
            if (lstPlaylists.SelectedItem is not PlaylistUrl seleccionada)
            {
                MessageBox.Show("Seleccioná una playlist.");
                return;
            }

            await _playlistService.AbrirYReproducirAsync(seleccionada.Url, forzar: true);
        }

        // ---------- Frases ----------

        private async void GuardarFuente_Click(object sender, RoutedEventArgs e)
        {
            var url = txtFuente.Text.Trim();

            if (!UrlValida(url))
            {
                MessageBox.Show("Ingresá una URL válida (http o https).");
                return;
            }

            var boton = (Button)sender;
            boton.IsEnabled = false;
            txtEstado.Text = "Descargando frases...";

            try
            {
                var fuente = _db.Fuentes.FirstOrDefault();
                if (fuente == null)
                    _db.Fuentes.Add(new FuenteFrases { Url = url });
                else
                    fuente.Url = url;
                _db.SaveChanges();

                var nuevas = await _frasesService.DescargarAsync(url);

                if (nuevas.Count > 0)
                {
                    _db.Frases.RemoveRange(_db.Frases.ToList());
                    _db.Frases.AddRange(nuevas.Select(t => new Frase { Texto = t }));
                    _db.SaveChanges();
                    txtEstado.Text = $"Listo: {nuevas.Count} frases cargadas.";
                }
                else
                {
                    txtEstado.Text = "Se guardó la página, pero no se pudieron leer frases. Se usarán las de respaldo.";
                }
            }
            finally
            {
                boton.IsEnabled = true;
            }
        }

        // ---------- Apariencia ----------

        private void ElegirFondo_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new OpenFileDialog
            {
                Title = "Elegir imagen de fondo",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dialogo.ShowDialog(this) != true) return;

            if (!TemaService.ElegirFondo(dialogo.FileName))
            {
                MessageBox.Show("No se pudo usar esa imagen. Probá con un archivo PNG o JPG.");
                return;
            }

            TemaService.Aplicar();
            ActualizarEstadoFondo();
        }

        private void QuitarFondo_Click(object sender, RoutedEventArgs e)
        {
            TemaService.QuitarFondo();
            TemaService.Aplicar();
            ActualizarEstadoFondo();
        }

        private void Transparencia_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;

            TemaService.Transparencia = (int)sldTransparencia.Value;
            ActualizarEtiquetaTransparencia();
            TemaService.Aplicar();
        }

        private void ActualizarEtiquetaTransparencia()
        {
            txtTransparencia.Text = $"Transparencia de los cuadros: {(int)sldTransparencia.Value}%";
        }

        private void ActualizarEstadoFondo()
        {
            var ruta = TemaService.RutaFondo;
            txtFondoEstado.Text = ruta == null
                ? "Sin imagen de fondo."
                : "Imagen de fondo activa.";
        }
    }
}