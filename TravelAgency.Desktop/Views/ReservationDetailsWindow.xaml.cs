using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;

namespace TravelAgency.Desktop.Views
{
    public partial class ReservationDetailsWindow : Window
    {
        private readonly TravelAgencyDbContext db;
        private readonly int _reservationId;

        public ReservationDetailsWindow(TravelAgencyDbContext db, int reservationId)
        {
            InitializeComponent();
            db = db; _reservationId = reservationId;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var r = await db.Reservations
                .Include(x => x.Customer)
                .Include(x => x.Items)!.ThenInclude(i => i.AllotmentRoomType)!.ThenInclude(art => art!.Allotment)!.ThenInclude(a => a!.Hotel)!.ThenInclude(h => h!.City)
                .Include(x => x.Payments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == _reservationId);

            var logs = await db.UpdateLogs
                .Where(x => x.EntityName == "Reservation" && x.EntityId == _reservationId)
                .OrderByDescending(x => x.ChangedAtUtc).Take(100)
                .AsNoTracking().ToListAsync();

            var items = r.Items.Select(i =>
            {
                if (i.AllotmentRoomType != null)
                {
                    var hotel = i.AllotmentRoomType.Allotment!.Hotel!;
                    var room = i.AllotmentRoomType.RoomType?.Name ?? "Room";
                    return $"{hotel.Name} • {room} × {i.Qty} @ {i.UnitPrice:0.##} {i.Currency}";
                }
                return $"{i.ServiceName} × {i.Qty} @ {i.UnitPrice:0.##} {i.Currency}";
            }).ToList();

            var payments = r.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => $"{p.PaymentDate:dd/MM/yyyy} • {p.Amount:0.##} ({p.Method}) {p.Notes}")
                .ToList();

            DataContext = new
            {
                r.Title,
                CustomerLine = $"Customer: {r.Customer!.Name}",
                DateRange = $"Dates: {r.StartDate:dd/MM/yyyy} – {r.EndDate:dd/MM/yyyy}",
                DueInfo = $"Deposit: {(r.DepositDueDate?.ToString("dd/MM/yyyy") ?? "n/a")}  |  Balance: {(r.BalanceDueDate?.ToString("dd/MM/yyyy") ?? "n/a")}",
                Status = $"Status: {r.Status}",
                r.Notes,
                CreatedUpdated = $"Created: {r.CreatedAt:u} | Updated: {r.UpdatedAt:u}",
                Items = items,
                Payments = payments,
                History = logs.Select(l => new
                {
                    Header = $"{l.ChangedAtUtc:u} • {l.PropertyName}",
                    Diff = $"{l.OldValue} → {l.NewValue}"
                }).ToList()
            };
        }
    }
}
