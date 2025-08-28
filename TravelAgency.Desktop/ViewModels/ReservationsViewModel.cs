using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Domain.Models;   // <-- add this
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class ReservationsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        // filters / pickers from cache
        public ObservableCollection<City> Cities => _cache.Cities;
        public ObservableCollection<Customer> Customers => _cache.Customers;

        // left filters
        [ObservableProperty] private City? selectedCity;
        [ObservableProperty] private DateTime fromDate = DateTime.Today;
        [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(30);

        // right composer header
        [ObservableProperty] private Customer? selectedCustomer;
        [ObservableProperty] private string? title;
        [ObservableProperty] private DateTime? resFrom = DateTime.Today;
        [ObservableProperty] private DateTime? resTo = DateTime.Today.AddDays(3);
        [ObservableProperty] private DateTime? depositDue;
        [ObservableProperty] private DateTime? balanceDue;
        [ObservableProperty] private string? notes;

        // available allotments (left list)
        public ObservableCollection<AvailableAllotmentDto> Available { get; } = new();

        // basket the user is building to be saved as reservation items
        public ObservableCollection<ReservationBasketLine> Basket { get; } = new();

        // service subform fields  (KEEP ONLY THIS BLOCK — delete duplicates)
        [ObservableProperty] private string? serviceName;
        [ObservableProperty] private string? serviceQty = "1";
        [ObservableProperty] private string? servicePrice = "0";
        [ObservableProperty] private string? serviceCurrency = "EUR";

        public ReservationsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        { _dbf = dbf; _cache = cache; }

        [RelayCommand]
        private async Task LoadAvailableAsync()
        {
            if (SelectedCustomer == null)
            {
                // optional: don't show availability until a customer is chosen
                Available.Clear();
                return;
            }

            await using var db = await _dbf.CreateDbContextAsync();

            // base lines (AllotmentRoomType) with hotel/city and date overlap
            var lines = db.AllotmentRoomTypes
                .Include(l => l.RoomType)
                .Include(l => l.Allotment)!.ThenInclude(a => a.Hotel)!.ThenInclude(h => h.City)
                .Where(l => l.Allotment!.EndDate >= FromDate &&
                            l.Allotment!.StartDate <= ToDate);

            if (SelectedCity != null)
                lines = lines.Where(l => l.Allotment!.Hotel!.CityId == SelectedCity.Id);

            // compute Sold per line (exclude cancelled reservations)
            var lineWithSold = from l in lines
                               join ri in db.ReservationItems.Include(x => x.Reservation)
                                    on l.Id equals ri.AllotmentRoomTypeId into g
                               select new
                               {
                                   Line = l,
                                   Sold = g.Where(x => x.Reservation.Status != ReservationStatus.Cancelled)
                                           .Sum(x => (int?)x.Qty) ?? 0
                               };

            // keep only lines with Remaining > 0
            var availableLines = await lineWithSold
                .Where(x => (x.Line.QuantityTotal - x.Line.QuantityCancelled - x.Sold) > 0)
                .AsNoTracking()
                .ToListAsync();

            // group by scalar fields (avoids blank/duplicate rows)
            var grouped = availableLines
                .GroupBy(x => new
                {
                    x.Line.AllotmentId,
                    x.Line.Allotment!.Title,
                    x.Line.Allotment!.StartDate,
                    x.Line.Allotment!.EndDate,
                    HotelName = x.Line.Allotment!.Hotel!.Name
                })
                .Select(g => new AvailableAllotmentDto
                {
                    AllotmentId = g.Key.AllotmentId,
                    Title = g.Key.Title,
                    HotelName = g.Key.HotelName,
                    StartDate = g.Key.StartDate,
                    EndDate = g.Key.EndDate,
                    RemainingTotal = g.Sum(x => x.Line.QuantityTotal - x.Line.QuantityCancelled - x.Sold)
                })
                .OrderBy(x => x.StartDate)
                .ToList();

            Available.Clear();
            foreach (var it in grouped) Available.Add(it);
        }



        [RelayCommand]
        private void AddServiceLine()
        {
            if (string.IsNullOrWhiteSpace(ServiceName)) return;

            if (!int.TryParse(ServiceQty ?? "1", out var qty)) qty = 1;
            if (!decimal.TryParse(ServicePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                price = 0m;

            Basket.Add(new ReservationBasketLine
            {
                Kind = "Service",
                Title = ServiceName!.Trim(),
                Qty = qty,
                UnitPrice = price,
                Currency = string.IsNullOrWhiteSpace(ServiceCurrency) ? "EUR" : ServiceCurrency!
            });

            // reset subform
            ServiceName = "";
            ServiceQty = "1";
            ServicePrice = "0";
            ServiceCurrency = "EUR";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedCustomer == null || string.IsNullOrWhiteSpace(Title) || ResFrom == null || ResTo == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

            var r = new Reservation
            {
                CustomerId = SelectedCustomer.Id,
                Title = Title!.Trim(),
                StartDate = ResFrom!.Value.Date,
                EndDate = ResTo!.Value.Date,
                DepositDueDate = DepositDue,
                BalanceDueDate = BalanceDue,
                Notes = Notes
                // Status left as default (no Active in your enum)
            };
            db.Reservations.Add(r);
            await db.SaveChangesAsync();

            foreach (var line in Basket)
            {
                if (line.Kind == "AllotmentRoom")
                {
                    db.ReservationItems.Add(new ReservationItem
                    {
                        ReservationId = r.Id,
                        Kind = ReservationItemKind.AllotmentRoom,
                        AllotmentRoomTypeId = line.AllotmentRoomTypeId,
                        Qty = line.Qty,
                        UnitPrice = line.UnitPrice,
                        Currency = line.Currency,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate
                    });
                }
                else
                {
                    db.ReservationItems.Add(new ReservationItem
                    {
                        ReservationId = r.Id,
                        Kind = ReservationItemKind.Service,
                        ServiceName = line.Title,
                        Qty = line.Qty,
                        UnitPrice = line.UnitPrice,
                        Currency = line.Currency
                    });
                }
            }

            await db.SaveChangesAsync();

            // reset composer
            Title = "";
            Notes = "";
            Basket.Clear();
        }
    }
}
