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
                .Include(x => x.Lines)!.ThenInclude(i => i.AllotmentRoomType)!.ThenInclude(art => art!.Allotment)!.ThenInclude(a => a!.Hotel)!.ThenInclude(h => h!.City)
                .Include(x => x.Payments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == _reservationId);

            var logs = await db.UpdateLogs
                .Where(x => x.EntityName == "Reservation" && x.EntityId == _reservationId)
                .OrderByDescending(x => x.ChangedAtUtc).Take(100)
                .AsNoTracking().ToListAsync();

            var items = r.Lines.Select(i =>
            {
                if (i.AllotmentRoomType != null)
                {
                    var hotel = i.AllotmentRoomType.Allotment!.Hotel!;
                    var room = i.AllotmentRoomType.RoomType?.Name ?? "Room";
                    return $"{hotel.Name} • {room} × {i.Quantity} @ {i.PricePerNight:0.##} €";
                }
                return $"{i.AllotmentRoomType?.RoomType?.Name??"error room type"} × {i.Quantity} @ {i.PricePerNight:0.##} €";
            }).ToList();

            var payments = r.Payments
                .OrderByDescending(p => p.Date)
                .Select(p => $"{p.Date:dd/MM/yyyy} • {p.Amount:0.##} ({p.Kind}) {p.Notes}")
                .ToList();
            var label = $"Reservation #{r.Id} – {r.CheckIn:yyyy-MM-dd} → {r.CheckOut:yyyy-MM-dd}";
            DataContext = new
            {
                label,
                CustomerLine = $"Customer: {r.Customer!.Name}",
                DateRange = $"Dates: {r.CheckIn:dd/MM/yyyy} – {r.CheckOut:dd/MM/yyyy}",
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
