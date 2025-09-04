using System;
using System.Threading.Tasks;
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

            // Λύνουμε DI από το ServiceProvider της εφαρμογής
            var sp = ((App)Application.Current).Services;
            var dbf = sp.GetRequiredService<IDbContextFactory<TravelAgencyDbContext>>();
            var cache = sp.GetRequiredService<LookupCacheService>();

            // Δημιούργησε το VM του editor με DI (πρόσθεσε τέτοιο ctor στο VM σου)
            var vm = new AllotmentEditorViewModel(dbf, cache);

            // Όταν ο VM πει "Close(true/false)" κλείσε με DialogResult
            vm.CloseRequested += ok =>
            {
                DialogResult = ok;
                Close();
            };

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
