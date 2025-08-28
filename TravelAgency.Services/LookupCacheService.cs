using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Services
{
    /// <summary>
    /// Preloads and shares lookups (Hotels, Customers, Cities, RoomTypes).
    /// Uses IUiDispatcher for thread-safe ObservableCollection updates.
    /// </summary>
    public class LookupCacheService
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly IUiDispatcher _ui;
        private bool _isWarmed;

        public ObservableCollection<Hotel> Hotels { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<City> Cities { get; } = new();
        public ObservableCollection<RoomType> RoomTypes { get; } = new();

        public event EventHandler? Refreshed;

        public LookupCacheService(IDbContextFactory<TravelAgencyDbContext> dbf, IUiDispatcher uiDispatcher)
        {
            _dbf = dbf;
            _ui = uiDispatcher;
        }

        public async Task WarmUpAsync()
        {
            if (_isWarmed) return;
            await RefreshAsync();
            _isWarmed = true;
        }

        public async Task RefreshAsync()
        {
            try
            {
                // small safety timeout so we don't hang forever
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                await using var db = await _dbf.CreateDbContextAsync(cts.Token);

                // One pass per list; all AsNoTracking and ordered
                var hotels = await db.Hotels.Include(x => x.City)
                                            .AsNoTracking()
                                            .OrderBy(x => x.Name)
                                            .ToListAsync(cts.Token);

                var customers = await db.Customers
                                        .AsNoTracking()
                                        .OrderBy(x => x.Name)
                                        .ToListAsync(cts.Token);

                var cities = await db.Cities
                                     .AsNoTracking()
                                     .OrderBy(x => x.Name)
                                     .ToListAsync(cts.Token);

                var roomTypes = await db.RoomTypes
                                        .AsNoTracking()
                                        .OrderBy(x => x.Name)
                                        .ToListAsync(cts.Token);

                void Apply()
                {
                    Hotels.Clear(); foreach (var h in hotels) Hotels.Add(h);
                    Customers.Clear(); foreach (var c in customers) Customers.Add(c);
                    Cities.Clear(); foreach (var ci in cities) Cities.Add(ci);
                    RoomTypes.Clear(); foreach (var rt in roomTypes) RoomTypes.Add(rt);

                    Refreshed?.Invoke(this, EventArgs.Empty);
                }

                if (!_ui.CheckAccess()) _ui.Invoke(Apply);
                else Apply();
            }
            catch (Exception ex)
            {
                // log to console for now (you can switch to Serilog or show a MessageBox)
                Console.Error.WriteLine($"LookupCacheService.RefreshAsync failed: {ex}");

                // rethrow or swallow depending on your tolerance
                throw;
            }
        }

    }
}
