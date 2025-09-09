using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;

namespace TravelAgency.Desktop.Views
{
    public partial class CustomerDetailsWindow : Window
    {
        private readonly TravelAgencyDbContext db;
        private readonly int _customerId;

        public CustomerDetailsWindow(TravelAgencyDbContext db, int customerId)
        {
            InitializeComponent();
            db = db; _customerId = customerId;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var c = await db.Customers.AsNoTracking().FirstAsync(x => x.Id == _customerId);

            var recentReservations = await db.Reservations
                .Where(r => r.CustomerId == _customerId)
                .OrderByDescending(r => r.UpdatedAt)
                .Take(10)
                .Select(r => r.Title)
                .ToListAsync();

            var logs = await db.UpdateLogs
                .Where(x => x.EntityName == "Customer" && x.EntityId == _customerId)
                .OrderByDescending(x => x.ChangedAtUtc)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();

            DataContext = new
            {
                c.Name,
                c.Email,
                c.Phone,
                OldBalance = $"Old balance: {c.OldBalance:0.##}",
                c.Notes,
                CreatedUpdated = $"Created: {c.CreatedAt:u} | Updated: {c.UpdatedAt:u}",
                Reservations = recentReservations,
                History = logs.Select(l => new
                {
                    Header = $"{l.ChangedAtUtc:u} • {l.PropertyName}",
                    Diff = $"{l.OldValue} → {l.NewValue}"
                }).ToList()
            };
        }
    }
}
