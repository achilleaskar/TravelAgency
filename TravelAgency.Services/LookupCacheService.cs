using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Services
{
    /// <summary>
    /// Preloads and shares lookups across the app (Hotels, Customers, Cities, RoomTypes).
    /// Bind ComboBoxes directly to these ObservableCollections.
    /// Call RefreshAsync() after you add/edit/delete any of these entities.
    /// </summary>
    public class LookupCacheService
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private bool _isWarmed;

        public ObservableCollection<Hotel> Hotels { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<City> Cities { get; } = new();
        public ObservableCollection<RoomType> RoomTypes { get; } = new();

        public LookupCacheService(IDbContextFactory<TravelAgencyDbContext> dbf)
        {
            _dbf = dbf;
        }

        public async Task WarmUpAsync()
        {
            if (_isWarmed) return;
            await RefreshAsync();
            _isWarmed = true;
        }

        public async Task RefreshAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Hotels.Clear();
            foreach (var h in await db.Hotels.Include(x => x.City)
                                             .AsNoTracking()
                                             .OrderBy(x => x.Name)
                                             .ToListAsync())
                Hotels.Add(h);

            Customers.Clear();
            foreach (var c in await db.Customers.AsNoTracking()
                                                .OrderBy(x => x.Name)
                                                .ToListAsync())
                Customers.Add(c);

            Cities.Clear();
            foreach (var ci in await db.Cities.AsNoTracking()
                                              .OrderBy(x => x.Name)
                                              .ToListAsync())
                Cities.Add(ci);

            RoomTypes.Clear();
            foreach (var rt in await db.RoomTypes.AsNoTracking()
                                                 .OrderBy(x => x.Name)
                                                 .ToListAsync())
                RoomTypes.Add(rt);
        }
    }
}
