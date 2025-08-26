using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using TravelAgency.Data;

namespace TravelAgency.Desktop.Views
{
    public partial class HotelDetailsWindow : Window
    {
        private readonly TravelAgencyDbContext _db;
        private readonly int _hotelId;

        public HotelDetailsWindow(TravelAgencyDbContext db, int hotelId)
        {
            InitializeComponent();
            _db = db; _hotelId = hotelId;
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var h = await _db.Hotels.Include(x => x.City).AsNoTracking().FirstAsync(x => x.Id == _hotelId);
            var logs = await _db.UpdateLogs
                .Where(x => x.EntityType == "Hotel" && x.EntityId == _hotelId)
                .OrderByDescending(x => x.ChangedAt)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();

            DataContext = new
            {
                h.Name,
                CityName = $"{h.City?.Name} ({h.City?.Country})",
                h.Address,
                h.Phone,
                h.Email,
                h.Notes,
                CreatedUpdated = $"Created: {h.CreatedAt:u} | Updated: {h.UpdatedAt:u}",
                History = logs.Select(l => new
                {
                    Header = $"{l.ChangedAt:u} • {l.Field}",
                    Diff = $"{l.OldValue} → {l.NewValue}"
                }).ToList()
            };
        }
    }
}
