using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using DespertadorApp.Data;

namespace DespertadorApp.Services
{
    public class SchedulerService
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
        private readonly DespertarService _despertar = new();
        private readonly Dictionary<int, DateTime> _ejecutadas = new();
        private DateTime _ultimaEjecucion = DateTime.MinValue;
        private bool _ocupado;

        public void Iniciar()
        {
            _timer.Tick += async (_, _) => await RevisarAsync();
            _timer.Start();
        }

        private async Task RevisarAsync()
        {
            if (_ocupado) return;

            var ahora = DateTime.Now;

            // Después de ejecutar, no vuelve a hacerlo por 10 minutos, sin importar la alarma
            if (ahora - _ultimaEjecucion < TimeSpan.FromMinutes(10)) return;

            _ocupado = true;

            try
            {
                using var db = new AppDbContext();
                var alarmas = db.Alarmas.Where(a => a.Activa).ToList();

                foreach (var a in alarmas)
                {
                    var dias = a.DiasSemana.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var hoyActiva = dias.Contains(((int)ahora.DayOfWeek).ToString());
                    var yaSono = _ejecutadas.TryGetValue(a.Id, out var fecha) && fecha.Date == ahora.Date;
                    var enHora = ahora.TimeOfDay >= a.Hora
                              && ahora.TimeOfDay < a.Hora + TimeSpan.FromMinutes(2);

                    if (hoyActiva && !yaSono && enHora)
                    {
                        _ultimaEjecucion = ahora;
                        _ejecutadas[a.Id] = ahora;
                        await _despertar.EjecutarAsync();
                        break;
                    }
                }
            }
            finally
            {
                _ocupado = false;
            }
        }
    }
}