using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Media;

namespace DespertadorApp.Services
{
    public record EstadoMedia(string Titulo, string Artista, bool Sonando, bool Disponible);

    public class MediaService
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;

        private async Task<GlobalSystemMediaTransportControlsSession?> ObtenerSesionAsync()
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            static bool EsMusica(GlobalSystemMediaTransportControlsSession s)
            {
                var id = s.SourceAppUserModelId ?? "";
                return id.Contains("chrome", StringComparison.OrdinalIgnoreCase)
                    || id.Contains("edge", StringComparison.OrdinalIgnoreCase)
                    || id.Contains("spotify", StringComparison.OrdinalIgnoreCase);
            }

            var candidatas = _manager.GetSessions().Where(EsMusica).ToList();

            return candidatas.FirstOrDefault(s =>
                       s.GetPlaybackInfo().PlaybackStatus ==
                       GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                   ?? candidatas.FirstOrDefault();
        }

        public async Task<EstadoMedia> ObtenerEstadoAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                if (sesion == null) return new EstadoMedia("", "", false, false);

                var props = await sesion.TryGetMediaPropertiesAsync();
                var sonando = sesion.GetPlaybackInfo().PlaybackStatus ==
                              GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                return new EstadoMedia(props.Title ?? "", props.Artist ?? "", sonando, true);
            }
            catch
            {
                return new EstadoMedia("", "", false, false);
            }
        }

        public async Task<bool> PlayPauseAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                return sesion != null && await sesion.TryTogglePlayPauseAsync();
            }
            catch { return false; }
        }

        // Reproducir (no alterna: solo da play)
        public async Task<bool> ReproducirAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                return sesion != null && await sesion.TryPlayAsync();
            }
            catch { return false; }
        }

        public async Task<bool> SiguienteAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                return sesion != null && await sesion.TrySkipNextAsync();
            }
            catch { return false; }
        }

        public async Task<bool> AnteriorAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                return sesion != null && await sesion.TrySkipPreviousAsync();
            }
            catch { return false; }
        }

        // Devuelve false si el reproductor no permite cambiar el modo aleatorio
        public async Task<bool> CambiarAleatorioAsync(bool activo)
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                if (sesion == null) return false;

                if (!sesion.GetPlaybackInfo().Controls.IsShuffleEnabled) return false;

                return await sesion.TryChangeShuffleActiveAsync(activo);
            }
            catch { return false; }
        }
        // "NONE", "ALL" (toda la playlist) o "ONE" (la canción). Devuelve null si el reproductor no lo expone.
        public async Task<string?> ObtenerRepeticionAsync()
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                if (sesion == null) return null;

                var info = sesion.GetPlaybackInfo();
                if (!info.Controls.IsRepeatEnabled || info.AutoRepeatMode == null) return null;

                return info.AutoRepeatMode.Value switch
                {
                    MediaPlaybackAutoRepeatMode.List => "ALL",
                    MediaPlaybackAutoRepeatMode.Track => "ONE",
                    _ => "NONE"
                };
            }
            catch { return null; }
        }

        public async Task<bool> CambiarRepeticionAsync(string modo)
        {
            try
            {
                var sesion = await ObtenerSesionAsync();
                if (sesion == null) return false;

                if (!sesion.GetPlaybackInfo().Controls.IsRepeatEnabled) return false;

                var nuevo = modo switch
                {
                    "ALL" => MediaPlaybackAutoRepeatMode.List,
                    "ONE" => MediaPlaybackAutoRepeatMode.Track,
                    _ => MediaPlaybackAutoRepeatMode.None
                };

                return await sesion.TryChangeAutoRepeatModeAsync(nuevo);
            }
            catch { return false; }
        }

    }

}