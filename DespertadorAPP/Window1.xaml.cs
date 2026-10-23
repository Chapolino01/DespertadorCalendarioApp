using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DespertadorApp.Data;
using DespertadorApp.Models;

namespace DespertadorApp
{
    public partial class AlarmasWindow : Window
    {
        private readonly AppDbContext _db = new();

        public AlarmasWindow()
        {
            InitializeComponent();
            CargarAlarmas();
        }

        // El índice coincide con DayOfWeek: 0=domingo ... 6=sábado
        private CheckBox[] Checks => new[] { chkDom, chkLun, chkMar, chkMie, chkJue, chkVie, chkSab };

        private void CargarAlarmas()
        {
            lstAlarmas.ItemsSource = null;
            lstAlarmas.ItemsSource = _db.Alarmas.ToList().OrderBy(a => a.Hora).ToList();
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            if (!TimeSpan.TryParseExact(txtHora.Text.Trim(), "h\\:mm", null, out var hora))
            {
                MessageBox.Show("Ingresá la hora con formato HH:mm (por ejemplo 07:30).");
                return;
            }

            var dias = Checks
                .Select((c, i) => new { c, i })
                .Where(x => x.c.IsChecked == true)
                .Select(x => x.i.ToString())
                .ToList();

            if (dias.Count == 0)
            {
                MessageBox.Show("Elegí al menos un día.");
                return;
            }

            _db.Alarmas.Add(new Alarma { Hora = hora, DiasSemana = string.Join(",", dias) });
            _db.SaveChanges();
            CargarAlarmas();
        }

        private void Alternar_Click(object sender, RoutedEventArgs e)
        {
            if (lstAlarmas.SelectedItem is not Alarma alarma)
            {
                MessageBox.Show("Seleccioná una alarma.");
                return;
            }

            alarma.Activa = !alarma.Activa;
            _db.SaveChanges();
            CargarAlarmas();
        }

        private void Eliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstAlarmas.SelectedItem is not Alarma alarma)
            {
                MessageBox.Show("Seleccioná una alarma.");
                return;
            }

            _db.Alarmas.Remove(alarma);
            _db.SaveChanges();
            CargarAlarmas();
        }
    }
}