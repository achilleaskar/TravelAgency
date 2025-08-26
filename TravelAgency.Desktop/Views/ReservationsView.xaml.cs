using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class ReservationsView : UserControl
    {
        public ReservationsView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<ReservationsViewModel>();
        }
    }
}
