using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DespertadorApp.Data;
using DespertadorApp.Models;
using DespertadorApp.Services;

namespace DespertadorApp
{
    public partial class EventosWindow : Window
    {
        private readonly AppDbContext _db = new();

        public EventosWindow()
        {
            InitializeComponent();
            CargarEventos();
        }

        private void CargarEventos()
        {
            lstEventos.ItemsSource = null;
            lstEventos.ItemsSource = _db.Eventos.ToList().OrderBy(ev => ev.Fecha).ToList();
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            var titulo = txtTitulo.Text.Trim();

            if (titulo == "")
            {
                MessageBox.Show("Ingresá un título.");
                return;
            }

            if (!DateTime.TryParseExact(txtFecha.Text.Trim(), new[] { "d/M/yyyy", "dd/MM/yyyy" },
                    new CultureInfo("es-AR"), DateTimeStyles.None, out var fecha))
            {
                MessageBox.Show("Ingresá la fecha con formato dd/mm/aaaa (por ejemplo 25/12/2026).");
                return;
            }

            if (!int.TryParse(txtDiasAntes.Text.Trim(), out var diasAntes) || diasAntes < 0 || diasAntes > 30)
            {
                MessageBox.Show("Los días de aviso deben ser un número entre 0 y 30.");
                return;
            }

            _db.Eventos.Add(new Evento
            {
                Titulo = titulo,
                Fecha = fecha.Date,
                DiasAntes = diasAntes
            });
            _db.SaveChanges();

            txtTitulo.Clear();
            txtFecha.Clear();
            CargarEventos();
        }

        private void Eliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstEventos.SelectedItem is not Evento evento)
            {
                MessageBox.Show("Seleccioná un evento.");
                return;
            }

            _db.Eventos.Remove(evento);
            _db.SaveChanges();
            CargarEventos();
        }

        private async void Probar_Click(object sender, RoutedEventArgs e)
        {
            var boton = (Button)sender;
            boton.IsEnabled = false;
            try
            {
                await new DespertarService().AvisarEventosAsync(_db, prueba: true);
            }
            finally
            {
                boton.IsEnabled = true;
            }
        }
    }
}