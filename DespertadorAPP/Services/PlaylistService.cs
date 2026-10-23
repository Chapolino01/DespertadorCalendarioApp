using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DespertadorApp.Services
{
    public class PlaylistService
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Puerto local (solo esta PC) que usa la app para controlar la pestaña de Chrome
        private const int PuertoControl = 9223;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
        private static DateTime _ultimaApertura = DateTime.MinValue;
        private static readonly TimeSpan TiempoSinReabrir = TimeSpan.FromHours(3);

        private static readonly string CarpetaDatos = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DespertadorApp");

        private static readonly string ArchivoAleatorio = Path.Combine(CarpetaDatos, "aleatorio.txt");
        private static readonly string ArchivoRepeticion = Path.Combine(CarpetaDatos, "repeticion.txt");

        private record Pestana(string Id, string WebSocketUrl);

        // URL de la última playlist que abrió la app (sirve para mostrar su nombre en pantalla)
        public static string UltimaUrl { get; private set; } = "";

        // Modo aleatorio: se guarda en un archivo para que se conserve al cerrar la app
        public static bool Aleatorio
        {
            get
            {
                try { return File.Exists(ArchivoAleatorio) && File.ReadAllText(ArchivoAleatorio).Trim() == "1"; }
                catch { return false; }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(CarpetaDatos);
                    File.WriteAllText(ArchivoAleatorio, value ? "1" : "0");
                }
                catch { }
            }
        }

        // Modo repetir: "NONE" (desactivado), "ALL" (toda la playlist) o "ONE" (la canción actual)
        public static string Repeticion
        {
            get
            {
                try
                {
                    var valor = File.Exists(ArchivoRepeticion) ? File.ReadAllText(ArchivoRepeticion).Trim() : "NONE";
                    return valor is "ALL" or "ONE" ? valor : "NONE";
                }
                catch { return "NONE"; }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(CarpetaDatos);
                    File.WriteAllText(ArchivoRepeticion, value);
                }
                catch { }
            }
        }

        public async Task AbrirYReproducirAsync(string url, bool forzar = false)
        {
            var uri = new Uri(url);
            var esYouTube = uri.Host.EndsWith("youtube.com");
            var reciente = DateTime.Now - _ultimaApertura < TiempoSinReabrir;
            var pestana = esYouTube ? await BuscarPestanaAsync() : null;

            // Alarmas: si ya se abrió hace poco y sigue abierta, no volver a abrir
            if (!forzar && reciente && (!esYouTube || pestana != null)) return;

            _ultimaApertura = DateTime.Now;
            UltimaUrl = url;

            if (esYouTube)
            {
                var destino = ConstruirDestinoYouTube(uri);

                if (pestana != null)
                {
                    // Ya hay una pestaña del reproductor: se reutiliza, sin abrir otra ventana.
                    // Se restaura la ventana porque minimizada no arranca la música.
                    await CambiarVentanaAsync(pestana, "normal");
                    await EnviarComandoAsync(pestana.WebSocketUrl, "Page.navigate", new { url = destino });
                    await EsperarReproduccionYMinimizarAsync();
                    return;
                }

                AbrirYouTube(destino);
                await EsperarReproduccionYMinimizarAsync();
                return;
            }

            // Spotify u otros
            Process.Start(new ProcessStartInfo(ConvertirSpotify(uri)) { UseShellExecute = true });
            await Task.Delay(8000);
            EnviarTecla(VK_MEDIA_PLAY_PAUSE);

            var media = new MediaService();
            if (Aleatorio)
                await media.CambiarAleatorioAsync(true);
            await media.CambiarRepeticionAsync(Repeticion);
        }

        public void AlternarPausa() => EnviarTecla(VK_MEDIA_PLAY_PAUSE);
        public void Siguiente() => EnviarTecla(VK_MEDIA_NEXT_TRACK);
        public void Anterior() => EnviarTecla(VK_MEDIA_PREV_TRACK);

        private static void EnviarTecla(byte tecla)
        {
            keybd_event(tecla, 0, 0, UIntPtr.Zero);
            keybd_event(tecla, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ---------- YouTube Music ----------

        private static string ConstruirDestinoYouTube(Uri uri)
        {
            var lista = HttpUtility.ParseQueryString(uri.Query)["list"];

            return string.IsNullOrEmpty(lista)
                ? uri.ToString()
                : $"https://music.youtube.com/watch?list={lista}" + (Aleatorio ? "&shuffle=1" : "");
        }

        private static void AbrirYouTube(string destino)
        {
            var perfil = Path.Combine(CarpetaDatos, "ChromePerfil");

            Process.Start(new ProcessStartInfo("chrome")
            {
                UseShellExecute = true,
                Arguments = $"--autoplay-policy=no-user-gesture-required --remote-debugging-port={PuertoControl} " +
                            $"--user-data-dir=\"{perfil}\" --new-window \"{destino}\""
            });
        }

        // Espera a que la música suene, aplica el modo repetir y recién ahí minimiza la ventana
        private static async Task EsperarReproduccionYMinimizarAsync()
        {
            var media = new MediaService();
            var pidioPlay = false;

            for (int i = 0; i < 25; i++)
            {
                await Task.Delay(1000);

                var estado = await media.ObtenerEstadoAsync();
                if (estado.Sonando)
                {
                    await AplicarRepeticionGuardadaAsync();

                    var pestana = await BuscarPestanaAsync();
                    if (pestana != null)
                        await CambiarVentanaAsync(pestana, "minimized");
                    return;
                }

                // Si a los 10 segundos no arrancó sola, se le pide reproducir
                if (i == 10 && !pidioPlay)
                {
                    pidioPlay = true;
                    await media.ReproducirAsync();
                }
            }

            // Si nunca arrancó, la ventana queda visible para que se note
        }

        private static async Task AplicarRepeticionGuardadaAsync()
        {
            // Si no se puede leer el estado del reproductor, no se toca nada
            if (await ObtenerRepeticionYouTubeAsync() == null) return;

            await EstablecerRepeticionYouTubeAsync(Repeticion);
        }

        // "NONE", "ALL", "ONE" o null si no se pudo leer
        public static async Task<string?> ObtenerRepeticionYouTubeAsync()
        {
            var pestana = await BuscarPestanaAsync();
            if (pestana == null) return null;

            var respuesta = await EnviarComandoAsync(pestana.WebSocketUrl, "Runtime.evaluate", new
            {
                expression = "document.querySelector('ytmusic-player-bar')?.getAttribute('repeat-mode_') ?? ''",
                returnByValue = true
            });

            var valor = LeerValor(respuesta)?.ToUpperInvariant() ?? "";

            if (valor.Contains("NONE")) return "NONE";
            if (valor.Contains("ALL")) return "ALL";
            if (valor.Contains("ONE")) return "ONE";
            return null;
        }

        // Toca el botón de repetir del reproductor hasta llegar al modo pedido
        public static async Task<bool> EstablecerRepeticionYouTubeAsync(string deseado)
        {
            for (int i = 0; i < 3; i++)
            {
                var actual = await ObtenerRepeticionYouTubeAsync();
                if (actual == deseado) return true;

                if (!await ClickRepeticionYouTubeAsync()) return false;

                // Si no se puede leer el estado, se asume un click por pulsación
                if (actual == null) return true;

                await Task.Delay(400);
            }

            return false;
        }

        private static async Task<bool> ClickRepeticionYouTubeAsync()
        {
            var pestana = await BuscarPestanaAsync();
            if (pestana == null) return false;

            var respuesta = await EnviarComandoAsync(pestana.WebSocketUrl, "Runtime.evaluate", new
            {
                expression = "(() => { const b = document.querySelector('ytmusic-player-bar .repeat'); if (!b) return false; b.click(); return true; })()",
                returnByValue = true
            });

            return LeerValor(respuesta)?.ToLowerInvariant() == "true";
        }

        private static string? LeerValor(string? respuesta)
        {
            if (respuesta == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(respuesta);
                return doc.RootElement
                    .GetProperty("result")
                    .GetProperty("result")
                    .GetProperty("value")
                    .ToString();
            }
            catch
            {
                return null;
            }
        }

        private static async Task<Pestana?> BuscarPestanaAsync()
        {
            try
            {
                var json = await _http.GetStringAsync($"http://127.0.0.1:{PuertoControl}/json");
                using var doc = JsonDocument.Parse(json);

                foreach (var t in doc.RootElement.EnumerateArray())
                {
                    var tipo = t.GetProperty("type").GetString();
                    var url = t.GetProperty("url").GetString() ?? "";

                    if (tipo == "page"
                        && url.Contains("music.youtube.com")
                        && t.TryGetProperty("webSocketDebuggerUrl", out var ws))
                    {
                        return new Pestana(t.GetProperty("id").GetString()!, ws.GetString()!);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        // estado: "minimized" o "normal"
        private static async Task CambiarVentanaAsync(Pestana pestana, string estado)
        {
            var respuesta = await EnviarComandoAsync(pestana.WebSocketUrl, "Browser.getWindowForTarget");
            if (respuesta == null) return;

            using var doc = JsonDocument.Parse(respuesta);
            if (!doc.RootElement.TryGetProperty("result", out var resultado)
                || !resultado.TryGetProperty("windowId", out var ventana))
                return;

            await EnviarComandoAsync(pestana.WebSocketUrl, "Browser.setWindowBounds",
                new { windowId = ventana.GetInt32(), bounds = new { windowState = estado } });
        }

        private static async Task<string?> EnviarComandoAsync(string wsUrl, string metodo, object? parametros = null)
        {
            try
            {
                using var ws = new ClientWebSocket();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await ws.ConnectAsync(new Uri(wsUrl), cts.Token);

                var mensaje = JsonSerializer.Serialize(new { id = 1, method = metodo, @params = parametros ?? new { } });
                await ws.SendAsync(Encoding.UTF8.GetBytes(mensaje), WebSocketMessageType.Text, true, cts.Token);

                var buffer = new byte[64 * 1024];
                while (true)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(buffer, cts.Token);
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    } while (!r.EndOfMessage);

                    var texto = sb.ToString();
                    using var doc = JsonDocument.Parse(texto);
                    if (doc.RootElement.TryGetProperty("id", out var id) && id.GetInt32() == 1)
                        return texto;
                }
            }
            catch
            {
                return null;
            }
        }

        // ---------- Spotify ----------

        private static string ConvertirSpotify(Uri uri)
        {
            if (uri.Host == "open.spotify.com")
            {
                var partes = uri.AbsolutePath
                    .Trim('/')
                    .Split('/')
                    .Where(p => !p.StartsWith("intl-"))
                    .ToArray();

                if (partes.Length >= 2)
                    return $"spotify:{partes[0]}:{partes[1]}";
            }
            return uri.ToString();
        }
    }
}