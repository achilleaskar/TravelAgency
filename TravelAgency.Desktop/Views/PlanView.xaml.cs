using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class PlanView : UserControl
    {
        public PlanView()
        {
            InitializeComponent();
            // Resolve από το Host του App
            DataContext = App.HostRef!.Services.GetRequiredService<PlanViewModel>();
        }
    }
}
