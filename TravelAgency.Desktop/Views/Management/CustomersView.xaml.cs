using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
            DataContext = App.HostRef!.Services.GetRequiredService<CustomersViewModel>();
        }

        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row) return;
            if (row.Item is not Customer customer) return;

            var db = App.HostRef!.Services.GetRequiredService<TravelAgencyDbContext>();
            var win = new CustomerDetailsWindow(db, customer.Id) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }
    }
}
