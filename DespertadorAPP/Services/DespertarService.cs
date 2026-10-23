using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DespertadorApp.Data;
using DespertadorApp.Models;

namespace DespertadorApp.Services
{
    public class DespertarService
    {
        private readonly VozService _voz = new();
        private readonly FrasesService _frases = new();
        private readonly PlaylistService _playlist = new();

        public async Task EjecutarAsync()
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();

            // 1. Actualizar frases desde la página configurada
            var fuente = db.Fuentes.FirstOrDefault();
            if (fuente != null)
            {
                var nuevas = await _frases.DescargarAsync(fuente.Url);
                if (nuevas.Count > 0)
                {
                    db.Frases.RemoveRange(db.Frases);
                    db.Frases.AddRange(nuevas.Select(t => new Frase { Texto = t }));
                    db.SaveChanges();
                }
            }

            // 2. Buen día + frase
            var frase = _frases.Aleatoria(db.Frases.Select(f => f.Texto).ToList());
            await _voz.DecirAsync($"Buen día. {frase}");

            // 3. Recordatorios de eventos próximos
            await AvisarEventosAsync(db);

            // 4. Playlist al azar
            var playlists = db.Playlists.ToList();
            if (playlists.Count > 0)
            {
                var elegida = playlists[Random.Shared.Next(playlists.Count)];
                await _playlist.AbrirYReproducirAsync(elegida.Url);
            }
        }

        public async Task AvisarEventosAsync(AppDbContext db, bool prueba = false)
        {
            var hoy = DateTime.Today;
            var es = new CultureInfo("es-AR");

            var eventos = db.Eventos.ToList()
                .Where(ev => ev.Fecha.Date >= hoy
                          && (ev.Fecha.Date - hoy).Days <= ev.DiasAntes
                          && (prueba || ev.UltimoAviso?.Date != hoy))
                .OrderBy(ev => ev.Fecha)
                .ToList();

            foreach (var ev in eventos)
            {
                var dias = (ev.Fecha.Date - hoy).Days;
                var cuando = dias == 0 ? "hoy"
                           : dias == 1 ? "mañana"
                           : "el " + ev.Fecha.ToString("dddd d 'de' MMMM", es);

                await _voz.DecirAsync($"Recuerda que {cuando} tienes {ev.Titulo}.");

                if (!prueba) ev.UltimoAviso = DateTime.Now;
            }

            if (!prueba && eventos.Count > 0) db.SaveChanges();
        }
    }
}