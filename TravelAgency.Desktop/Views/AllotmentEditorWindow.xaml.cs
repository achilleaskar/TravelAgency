using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Services;

namespace TravelAgency.Desktop.Views
{
    public partial class AllotmentEditorWindow : Window
    {
        public AllotmentEditorWindow(int? allotmentId)
        {
            InitializeComponent();

            var sp = App.HostRef!.Services;
            var dbf = sp.GetRequiredService<IDbContextFactory<TravelAgencyDbContext>>();
            var cache = sp.GetRequiredService<LookupCacheService>();
            var svc = sp.GetRequiredService<IAllotmentService>(); // optional but handy

            var vm = new AllotmentEditorViewModel(dbf, cache, svc);
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
