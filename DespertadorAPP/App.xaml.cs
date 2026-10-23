using System.Threading;
using System.Windows;
using DespertadorApp.Services;

namespace DespertadorApp
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "DespertadorApp_InstanciaUnica", out bool esNueva);

            if (!esNueva)
            {
                MessageBox.Show("La app ya está abierta.");
                Shutdown();
                return;
            }

            TemaService.Aplicar();
            base.OnStartup(e);
        }
    }
}