using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;

namespace TravelAgency.Desktop.Views
{
    public partial class AllotmentDetailsWindow : Window
    {
        private readonly TravelAgencyDbContext _db;
        private readonly int _allotmentId;

        public AllotmentDetailsWindow(TravelAgencyDbContext db, int allotmentId)
        {
            InitializeComponent();
            _db = db; _allotmentId = allotmentId;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var a = await _db.Allotments
                .Include(x => x.Hotel)!.ThenInclude(h => h.City)
                .Include(x => x.RoomTypes)!.ThenInclude(rt => rt.RoomType)
                .AsNoTracking()
                .FirstAsync(x => x.Id == _allotmentId);

            var logs = await _db.UpdateLogs
                .Where(x => x.EntityType == "Allotment" && x.EntityId == _allotmentId)
                .OrderByDescending(x => x.ChangedAt)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();

            DataContext = new
            {
                a.Title,
                HotelLine = $"{a.Hotel!.Name} • {a.Hotel.City!.Name} ({a.Hotel.City.Country})",
                DateRange = $"Dates: {a.StartDate:dd/MM/yyyy} – {a.EndDate:dd/MM/yyyy}",
                OptionInfo = a.OptionDueDate == null ? "Option: n/a" : $"Option due: {a.OptionDueDate:dd/MM/yyyy}",
                Status = $"Status: {a.Status}",
                a.Notes,
                CreatedUpdated = $"Created: {a.CreatedAt:u} | Updated: {a.UpdatedAt:u}",
                RoomTypes = a.RoomTypes
                    .Select(rt => $"{rt.RoomType!.Name}: {rt.Quantity} × {rt.PricePerNight:0.##} {rt.Currency}")
                    .ToList(),
                History = logs.Select(l => new
                {
                    Header = $"{l.ChangedAt:u} • {l.Field}",
                    Diff = $"{l.OldValue} → {l.NewValue}"
                }).ToList()
            };
        }
    }
}
