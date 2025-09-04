using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TravelAgency.Data;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.Views
{
    public partial class AllotmentDetailsWindow : Window
    {
        private readonly TravelAgencyDbContext db;

        public AllotmentDetailsWindow(TravelAgencyDbContext db, int allotmentId)
        {
            InitializeComponent();
            db = db; 
            _allotmentId = allotmentId;
            _ = LoadAsync();
        }

        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly int _allotmentId;

        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var a = await db.Allotments
                .Include(x => x.Hotel)!.ThenInclude(h => h.City)
                .Include(x => x.RoomTypes)!.ThenInclude(rt => rt.RoomType)
                .AsNoTracking()
                .FirstAsync(x => x.Id == _allotmentId);

            // compute Sold per line (exclude cancelled reservations)
            var lineIds = a.RoomTypes.Select(rt => rt.Id).ToList();

            var soldByLine = await db.ReservationItems
                .Include(ri => ri.Reservation)
                .Where(ri => ri.AllotmentRoomTypeId != null &&
                             lineIds.Contains(ri.AllotmentRoomTypeId.Value) &&
                             ri.Reservation.Status != ReservationStatus.Cancelled)
                .GroupBy(ri => ri.AllotmentRoomTypeId!.Value)
                .Select(g => new { LineId = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToDictionaryAsync(x => x.LineId, x => x.Qty);

            var logs = await db.UpdateLogs
                .Where(x => x.EntityName == "Allotment" && x.EntityId == _allotmentId)
                .OrderByDescending(x => x.ChangedAt)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();

            var roomTypeLines = a.RoomTypes
                .Select(rt =>
                {
                    var sold = soldByLine.TryGetValue(rt.Id, out var q) ? q : 0;
                    var baseCapacity = Math.Max(0, rt.Quantity);
                    var remaining = Math.Max(0, baseCapacity - sold);
                    return $"{rt.RoomType!.Name}: Total {rt.Quantity}, " +
                           $"Sold {sold}, Remaining {remaining} @ {rt.PricePerNight:0.##} {rt.Currency}";
                })
                .ToList();

            DataContext = new
            {
                a.Title,
                HotelLine = $"{a.Hotel!.Name} • {a.Hotel.City!.Name} ({a.Hotel.City.Country})",
                DateRange = $"Dates: {a.StartDate:dd/MM/yyyy} – {a.EndDate:dd/MM/yyyy}",
                OptionInfo = a.OptionDueDate == null ? "Option: n/a" : $"Option due: {a.OptionDueDate:dd/MM/yyyy}",
                Status = $"Status: {a.Status}",
                a.Notes,
                CreatedUpdated = $"Created: {a.CreatedAt:u} | Updated: {a.UpdatedAt:u}",
                RoomTypes = roomTypeLines,
                History = logs.Select(l => new
                {
                    Header = $"{l.ChangedAt:u} • {l.PropertyName}",
                    Diff = $"{l.OldValue} → {l.NewValue}"
                }).ToList()
            };
        }
    }
}
