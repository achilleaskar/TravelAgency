using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.Views
{
    public partial class ReservationsView : UserControl
    {
        public ReservationsView()
        {
            InitializeComponent();
        }

        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row) return;
            if (row.Item is not Reservation entity) return;

            var db = App.HostRef!.Services.GetRequiredService<TravelAgencyDbContext>();
            var win = new ReservationDetailsWindow(db, entity.Id) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }
    }
}
