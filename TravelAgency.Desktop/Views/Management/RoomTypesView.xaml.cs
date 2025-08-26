using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class RoomTypesView : UserControl
    {
        private bool _first = true;

        public RoomTypesView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<RoomTypesViewModel>();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object s, RoutedEventArgs e)
        {
            if (_first && DataContext is RoomTypesViewModel vm && vm.LoadCommand.CanExecute(null))
            { _first = false; await vm.LoadCommand.ExecuteAsync(null); }
        }
    }
}