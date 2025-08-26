using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class HotelsView : UserControl
    {
        private bool _first = true;

        public HotelsView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<HotelsViewModel>();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object s, RoutedEventArgs e)
        {
            if (_first && DataContext is HotelsViewModel vm && vm.LoadCommand.CanExecute(null))
            { _first = false; await vm.LoadCommand.ExecuteAsync(null); }
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