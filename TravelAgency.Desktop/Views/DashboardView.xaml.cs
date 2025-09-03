using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<DashboardViewModel>();
        }
    }
}
