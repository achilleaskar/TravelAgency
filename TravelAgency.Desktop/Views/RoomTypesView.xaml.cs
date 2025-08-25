using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class RoomTypesView : UserControl
    {
        public RoomTypesView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<RoomTypesViewModel>();
        }
    }
}
