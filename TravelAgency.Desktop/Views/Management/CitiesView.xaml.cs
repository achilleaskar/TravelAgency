using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class CitiesView : UserControl
    {
        private bool _first = true;
        public CitiesView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<CitiesViewModel>();
            Loaded += OnLoaded;
        }
        private async void OnLoaded(object s, System.Windows.RoutedEventArgs e)
        {
            if (_first && DataContext is CitiesViewModel vm && vm.LoadCommand.CanExecute(null))
            { _first = false; await vm.LoadCommand.ExecuteAsync(null); }
        }
    }
}
