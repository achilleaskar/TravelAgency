using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Data;

namespace TravelAgency.Desktop.Views
{
    public partial class HotelsView : UserControl
    {
        public HotelsView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<TravelAgency.Desktop.ViewModels.HotelsViewModel>();
        }

        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not TravelAgency.Desktop.ViewModels.HotelsViewModel vm) return;
            if (vm.Selected == null) return;

            // Resolve DbContext and show the detail window
            var db = App.HostRef!.Services.GetRequiredService<TravelAgency.Data.TravelAgencyDbContext>();
            var win = new HotelDetailsWindow(db, vm.Selected.Id) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }
    }
}
