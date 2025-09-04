using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Desktop.ViewModels;

namespace TravelAgency.Desktop.Views
{
    public partial class AllotmentEditorWindow : Window
    {
        public AllotmentEditorWindow(int? allotmentId)
        {
            InitializeComponent();

            // Πάρε το ServiceProvider από το App.HostRef
            var sp = App.HostRef!.Services; // <-- αυτό αντί για ((App)Application.Current).Services

            // Πάρε το IAllotmentService από DI
            var svc = sp.GetRequiredService<IAllotmentService>();

            var vm = new AllotmentEditorViewModel(svc);
            vm.CloseRequested += ok => { DialogResult = ok; Close(); };
            DataContext = vm;

            Loaded += async (_, __) =>
            {
                if (allotmentId.HasValue)
                    await vm.InitializeForEditAsync(allotmentId.Value);
                else
                    await vm.InitializeAsNewAsync();
            };
        }
    }
}