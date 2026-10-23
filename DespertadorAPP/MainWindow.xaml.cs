using DespertadorApp.Data;
using DespertadorApp.Models;
using DespertadorApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DespertadorApp
{
    public partial class MainWindow : Window
    {
        private static readonly string[] Dias = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
        private readonly CultureInfo _es = new("es-AR");

        private readonly PlaylistService _playlistService = new();
        private readonly MediaService _media = new();
        private readonly FrasesService _frases = new();
        private readonly SchedulerService _scheduler = new();

        private readonly DispatcherTimer _timerReloj = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _timerOnda = new() { Interval = TimeSpan.FromMilliseconds(140) };
        private readonly DispatcherTimer _timerMedia = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly List<Rectangle> _barras = new();
        private readonly Random _rnd = new();

        private List<PlaylistUrl> _playlists = new();
        private int _indice;
        private int _ultimoMinuto = -1;
        private bool _sonando;
        private bool _hayMedia;
        private bool _lanzarProxima;
        private bool _consultando;
        private string _repeticionVista = PlaylistService.Repeticion;
        private DateTime _ignorarRepeticionHasta = DateTime.MinValue;
        private int _tickRepeticion;

        public MainWindow()
        {
            InitializeComponent();

            using (var db = new AppDbContext())
                db.Database.EnsureCreated();

            CargarPlaylists();
            MostrarFrase();
            CargarSemana();
            ActualizarBotonAleatorio();
            ActualizarBotonRepetir();
            Reloj_Tick(null, EventArgs.Empty);

            _timerReloj.Tick += Reloj_Tick;
            _timerReloj.Start();

            _timerOnda.Tick += (_, _) => { if (_sonando) ActualizarOnda(); };
            _timerOnda.Start();

            _timerMedia.Tick += async (_, _) => await ActualizarEstadoAsync();
            _timerMedia.Start();

            _scheduler.Iniciar();
        }

        // ---------- Reloj y próxima alarma ----------

        private void Reloj_Tick(object? sender, EventArgs e)
        {
            var ahora = DateTime.Now;
            txtHora.Text = ahora.ToString("HH:mm");

            var fecha = ahora.ToString("dddd d 'de' MMMM", _es);
            txtFecha.Text = char.ToUpper(fecha[0]) + fecha.Substring(1);

            if (ahora.Minute != _ultimoMinuto)
            {
                _ultimoMinuto = ahora.Minute;
                CargarProximaAlarma();

                if (ahora.Hour == 0 && ahora.Minute == 0)
                    CargarSemana();
            }
        }

        private DateTime? CalcularProximaAlarma()
        {
            var ahora = DateTime.Now;
            DateTime? mejor = null;

            using var db = new AppDbContext();
            foreach (var a in db.Alarmas.AsNoTracking().Where(x => x.Activa).ToList())
            {
                var dias = a.DiasSemana
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToList();

                for (int d = 0; d <= 7; d++)
                {
                    var fecha = ahora.Date.AddDays(d) + a.Hora;
                    if (fecha > ahora && dias.Contains((int)fecha.DayOfWeek))
                    {
                        if (mejor == null || fecha < mejor) mejor = fecha;
                        break;
                    }
                }
            }

            return mejor;
        }

        private void CargarProximaAlarma()
        {
            var fecha = CalcularProximaAlarma();

            if (fecha == null)
            {
                txtProximaHora.Text = "--:--";
                txtProximaDetalle.Text = "Sin alarmas activas";
                return;
            }

            txtProximaHora.Text = fecha.Value.ToString("HH:mm");

            var dias = (fecha.Value.Date - DateTime.Today).Days;
            txtProximaDetalle.Text = dias == 0 ? "Hoy"
                                   : dias == 1 ? "Mañana"
                                   : Dias[(int)fecha.Value.DayOfWeek];
        }

        // ---------- Frase ----------

        private void MostrarFrase()
        {
            using var db = new AppDbContext();
            var frases = db.Frases.AsNoTracking().Select(f => f.Texto).ToList();
            txtFrase.Text = _frases.Aleatoria(frases);
        }

        private void OtraFrase_Click(object sender, RoutedEventArgs e) => MostrarFrase();

        // ---------- Semana ----------

        private void CargarSemana()
        {
            List<Evento> eventos;
            using (var db = new AppDbContext())
                eventos = db.Eventos.AsNoTracking().ToList();

            var hoy = DateTime.Today;
            var domingo = hoy.AddDays(-(int)hoy.DayOfWeek);

            var acento = (Brush)FindResource("Acento");
            var borde = (Brush)FindResource("Borde");
            var secundario = (Brush)FindResource("TextoSecundario");
            var principal = (Brush)FindResource("TextoPrincipal");

            gridSemana.Children.Clear();

            for (int i = 0; i < 7; i++)
            {
                var dia = domingo.AddDays(i);
                var esHoy = dia == hoy;

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock
                {
                    Text = $"{Dias[i]} {dia.Day}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = esHoy ? acento : secundario,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                foreach (var ev in eventos.Where(x => x.Fecha.Date == dia).OrderBy(x => x.Titulo))
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = "• " + ev.Titulo,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = principal,
                        FontSize = 13,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                }

                var celdaBorde = new Border
                {
                    BorderBrush = esHoy ? acento : borde,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10),
                    Margin = new Thickness(4),
                    MinHeight = 120,
                    Child = panel
                };
                celdaBorde.SetResourceReference(Border.BackgroundProperty, "TarjetaInterna");
                gridSemana.Children.Add(celdaBorde);
            }
        }

        // ---------- Reproductor ----------

        private void CargarPlaylists()
        {
            using var db = new AppDbContext();
            _playlists = db.Playlists.AsNoTracking().OrderBy(p => p.Id).ToList();

            if (_indice >= _playlists.Count) _indice = 0;
            MostrarPlaylist();
        }

        private void MostrarPlaylist()
        {
            if (_playlists.Count == 0)
            {
                txtPlaylist.Text = "Sin playlists";
                txtPlaylistInfo.Text = "Agregalas en Configuración";
                return;
            }

            txtPlaylist.Text = _playlists[_indice].Nombre;
            txtPlaylistInfo.Text = $"Playlist {_indice + 1} de {_playlists.Count}";
        }

        private async Task ActualizarEstadoAsync()
        {
            if (_consultando) return;
            _consultando = true;

            try
            {
                var estado = await _media.ObtenerEstadoAsync();

                _hayMedia = estado.Disponible;
                _sonando = estado.Sonando;
                btnPlay.Content = _sonando ? "\uE769" : "\uE768"; // pausa / play

                if (!estado.Disponible || string.IsNullOrWhiteSpace(estado.Titulo))
                    txtCancion.Text = "Sin reproducción";
                else
                    txtCancion.Text = string.IsNullOrWhiteSpace(estado.Artista)
                        ? estado.Titulo
                        : $"{estado.Titulo} · {estado.Artista}";

                // Si una alarma abrió otra playlist, mostrar su nombre
                if (!_lanzarProxima && !string.IsNullOrEmpty(PlaylistService.UltimaUrl))
                {
                    var i = _playlists.FindIndex(p => p.Url == PlaylistService.UltimaUrl);
                    if (i >= 0 && i != _indice)
                    {
                        _indice = i;
                        MostrarPlaylist();
                    }
                }

                ActualizarOnda();

                // Sincroniza el botón de repetir con lo que realmente tiene el reproductor
                if (_hayMedia && DateTime.Now >= _ignorarRepeticionHasta && ++_tickRepeticion % 3 == 0)
                {
                    var actual = await _media.ObtenerRepeticionAsync()
                              ?? await PlaylistService.ObtenerRepeticionYouTubeAsync();

                    if (actual != null && actual != _repeticionVista)
                    {
                        _repeticionVista = actual;
                        ActualizarBotonRepetir();
                    }
                }
            }
            finally
            {
                _consultando = false;
            }
        }

        private void CambiarPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_playlists.Count == 0) return;

            _indice = (_indice + 1) % _playlists.Count;
            _lanzarProxima = true; // el próximo Play abre la playlist elegida
            MostrarPlaylist();
        }

        private async Task LanzarAsync()
        {
            if (_playlists.Count == 0)
            {
                MessageBox.Show("Agregá una playlist desde Configuración.");
                return;
            }

            _lanzarProxima = false;
            await _playlistService.AbrirYReproducirAsync(_playlists[_indice].Url, forzar: true);
        }

        private async void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_lanzarProxima || !_hayMedia)
            {
                await LanzarAsync();
                return;
            }

            if (!await _media.PlayPauseAsync())
                _playlistService.AlternarPausa();
        }

        private async void Anterior_Click(object sender, RoutedEventArgs e)
        {
            if (!await _media.AnteriorAsync())
                _playlistService.Anterior();
        }

        private async void Siguiente_Click(object sender, RoutedEventArgs e)
        {
            if (!await _media.SiguienteAsync())
                _playlistService.Siguiente();
        }

        private async void Aleatorio_Click(object sender, RoutedEventArgs e)
        {
            PlaylistService.Aleatorio = !PlaylistService.Aleatorio;
            ActualizarBotonAleatorio();

            if (!_hayMedia) return;

            // Intenta cambiarlo en vivo. Si el reproductor no lo permite (YouTube Music),
            // queda guardado y se aplica la próxima vez que se abra la playlist.
            if (!await _media.CambiarAleatorioAsync(PlaylistService.Aleatorio))
                txtPlaylistInfo.Text = "Aleatorio guardado: se aplica al abrir la playlist";
        }

        private void ActualizarBotonAleatorio()
        {
            var activo = PlaylistService.Aleatorio;
            btnAleatorio.Foreground = (Brush)FindResource(activo ? "Acento" : "TextoSecundario");
            btnAleatorio.ToolTip = activo ? "Aleatorio: activado" : "Aleatorio: desactivado";
        }

        private async void Repetir_Click(object sender, RoutedEventArgs e)
        {
            var siguiente = _repeticionVista switch
            {
                "NONE" => "ALL",
                "ALL" => "ONE",
                _ => "NONE"
            };

            _repeticionVista = siguiente;
            PlaylistService.Repeticion = siguiente;
            ActualizarBotonRepetir();

            if (!_hayMedia) return;

            _ignorarRepeticionHasta = DateTime.Now.AddSeconds(5);

            var ok = await _media.CambiarRepeticionAsync(siguiente)
                  || await PlaylistService.EstablecerRepeticionYouTubeAsync(siguiente);

            if (!ok)
                txtPlaylistInfo.Text = "No pude cambiar el repetir del reproductor";
        }

        private void ActualizarBotonRepetir()
        {
            var activo = _repeticionVista != "NONE";

            icoRepetir.Foreground = (Brush)FindResource(activo ? "Acento" : "TextoSecundario");
            puntoRepetir.Visibility = _repeticionVista == "ALL" ? Visibility.Visible : Visibility.Collapsed;
            unoRepetir.Visibility = _repeticionVista == "ONE" ? Visibility.Visible : Visibility.Collapsed;

            btnRepetir.ToolTip = _repeticionVista switch
            {
                "ALL" => "Repetir: toda la playlist",
                "ONE" => "Repetir: esta canción",
                _ => "Repetir: desactivado"
            };
        }

        // ---------- Onda ----------

        private void Onda_SizeChanged(object sender, SizeChangedEventArgs e) => ConstruirOnda();

        private void ConstruirOnda()
        {
            cvOnda.Children.Clear();
            _barras.Clear();

            var cantidad = (int)(cvOnda.ActualWidth / 8);
            var acento = (Brush)FindResource("Acento");

            for (int i = 0; i < cantidad; i++)
            {
                var barra = new Rectangle
                {
                    Width = 4,
                    Height = 4,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = acento
                };
                Canvas.SetLeft(barra, i * 8);
                cvOnda.Children.Add(barra);
                _barras.Add(barra);
            }

            ActualizarOnda();
        }

        private void ActualizarOnda()
        {
            var alto = cvOnda.ActualHeight;

            for (int i = 0; i < _barras.Count; i++)
            {
                double h = 4;

                if (_sonando)
                {
                    var envolvente = Math.Sin(Math.PI * i / Math.Max(1, _barras.Count - 1));
                    h = 6 + envolvente * _rnd.NextDouble() * (alto - 10);
                }

                _barras[i].Height = h;
                Canvas.SetTop(_barras[i], (alto - h) / 2);
            }
        }

        // ---------- Botones inferiores ----------

        private void Alarmas_Click(object sender, RoutedEventArgs e)
        {
            new AlarmasWindow { Owner = this }.ShowDialog();
            CargarProximaAlarma();
        }

        private void Eventos_Click(object sender, RoutedEventArgs e)
        {
            new EventosWindow { Owner = this }.ShowDialog();
            CargarSemana();
        }

        private void Configuracion_Click(object sender, RoutedEventArgs e)
        {
            new ConfiguracionWindow { Owner = this }.ShowDialog();
            CargarPlaylists();
            MostrarFrase();
        }

        private async void Despertar_Click(object sender, RoutedEventArgs e)
        {
            var boton = (Button)sender;
            boton.IsEnabled = false;
            try
            {
                await new DespertarService().EjecutarAsync();
            }
            finally
            {
                boton.IsEnabled = true;
            }
        }
    }
}