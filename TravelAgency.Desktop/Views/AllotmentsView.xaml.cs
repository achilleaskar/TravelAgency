using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class AllotmentsView : UserControl
    {
        private bool _first = true;

        public AllotmentsView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<AllotmentsViewModel>();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object s, System.Windows.RoutedEventArgs e)
        {
            if (_first && DataContext is AllotmentsViewModel vm && vm.LoadCommand.CanExecute(null))
            {
                _first = false;
                await vm.LoadCommand.ExecuteAsync(null);
            }
        }

        private async void AllotmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AllotmentsViewModel vm &&
                vm.OpenEditDialogCommand.CanExecute(null))
            {
                await vm.OpenEditDialogCommand.ExecuteAsync(null);
            }
        }
    }
}
