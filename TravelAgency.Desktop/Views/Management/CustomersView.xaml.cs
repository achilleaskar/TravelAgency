using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<CustomersViewModel>();
        }
    }
}
