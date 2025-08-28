using System.Windows;
using TravelAgency.Services;

namespace TravelAgency.Desktop.Infrastructure
{
    public class WpfUiDispatcher : IUiDispatcher
    {
        public void Invoke(Action action)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) { action(); return; }         // no UI yet: just run
            if (disp.CheckAccess()) action();
            else disp.Invoke(action);
        }

        public bool CheckAccess()
        {
            var disp = Application.Current?.Dispatcher;
            return disp?.CheckAccess() ?? false;            // <- important: treat no UI as false
        }
    }
}
