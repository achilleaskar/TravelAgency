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

namespace TravelAgency.Desktop.ViewModels
{
    public class AvailableRoomVM
    {
        public int AllotmentRoomTypeId { get; set; }
        public string Hotel { get; set; } = "";
        public string RoomType { get; set; } = "";
        public int Free { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "EUR";
        public string Range { get; set; } = "";
    }

    public class BasketLineVM
    {
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public int? AllotmentRoomTypeId { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "EUR";
    }

    public partial class ReservationsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        public ReservationsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

        // left filters
        public ObservableCollection<City> Cities { get; } = new();
        [ObservableProperty] private City? filterCity;
        [ObservableProperty] private DateTime filterFrom = DateTime.Today;
        [ObservableProperty] private DateTime filterTo = DateTime.Today.AddDays(7);
        [ObservableProperty] private string? searchText;

        public ObservableCollection<AvailableRoomVM> Available { get; } = new();
        [ObservableProperty] private AvailableRoomVM? selectedAvailable;
        [ObservableProperty] private string addQty = "1";

        // right reservation form
        public ObservableCollection<Customer> Customers { get; } = new();
        [ObservableProperty] private Customer? selectedCustomer;
        [ObservableProperty] private string? title;
        [ObservableProperty] private DateTime? resFrom = DateTime.Today;
        [ObservableProperty] private DateTime? resTo = DateTime.Today.AddDays(3);
        [ObservableProperty] private DateTime? depositDue;
        [ObservableProperty] private DateTime? balanceDue;
        [ObservableProperty] private string? notes;

        public ObservableCollection<BasketLineVM> Basket { get; } = new();
        [ObservableProperty] private BasketLineVM? selectedLine;

        // add service subform
        [ObservableProperty] private string? serviceName;
        [ObservableProperty] private string? serviceQty = "1";
        [ObservableProperty] private string? servicePrice = "0";
        [ObservableProperty] private string? serviceCurrency = "EUR";
         

        [RelayCommand]
        private async Task LoadAvailableAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Available.Clear();

            var q = db.AllotmentRoomTypes
                .Include(art => art.Allotment)!.ThenInclude(a => a!.Hotel)!.ThenInclude(h => h.City)
                .Include(art => art.RoomType)
                .Where(art => art.Allotment!.StartDate <= FilterTo && art.Allotment!.EndDate >= FilterFrom);

            if (FilterCity != null)
                q = q.Where(art => art.Allotment!.Hotel!.CityId == FilterCity.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(art => art.Allotment!.Hotel!.Name.Contains(SearchText) ||
                                   art.RoomType!.Name.Contains(SearchText));

            var list = await q.AsNoTracking().ToListAsync();

            // compute free qty (simple: total - reserved across overlapping days)
            foreach (var art in list)
            {
                var reserved = await db.ReservationItems
                    .Where(x => x.AllotmentRoomTypeId == art.Id && x.Reservation!.Status != ReservationStatus.Cancelled)
                    .SumAsync(x => (int?)x.Qty) ?? 0;

                var free = Math.Max(0, art.Quantity - reserved);

                Available.Add(new AvailableRoomVM
                {
                    AllotmentRoomTypeId = art.Id,
                    Hotel = art.Allotment!.Hotel!.Name,
                    RoomType = art.RoomType!.Name,
                    Free = free,
                    Price = art.PricePerNight,
                    Currency = art.Currency,
                    Range = $"{art.Allotment!.StartDate:dd/MM}-{art.Allotment!.EndDate:dd/MM}"
                });
            }

            if (Cities.Count == 0)
            {
                foreach (var c in await db.Cities.OrderBy(x => x.Name).ToListAsync()) Cities.Add(c);
            }
            if (Customers.Count == 0)
            {
                foreach (var c in await db.Customers.OrderBy(x => x.Name).ToListAsync()) Customers.Add(c);
            }
        }

        [RelayCommand]
        private void AddAvailable()
        {
            if (SelectedAvailable == null) return;
            if (!int.TryParse(AddQty ?? "1", out var qty) || qty <= 0) qty = 1;

            Basket.Add(new BasketLineVM
            {
                Kind = "AllotmentRoom",
                Title = $"{SelectedAvailable.Hotel} • {SelectedAvailable.RoomType}",
                AllotmentRoomTypeId = SelectedAvailable.AllotmentRoomTypeId,
                Qty = qty,
                UnitPrice = SelectedAvailable.Price,
                Currency = SelectedAvailable.Currency
            });
        }

        [RelayCommand]
        private void AddService()
        {
            if (string.IsNullOrWhiteSpace(ServiceName)) return;
            if (!int.TryParse(ServiceQty ?? "1", out var qty) || qty <= 0) qty = 1;
            if (!decimal.TryParse(ServicePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price)) price = 0m;

            Basket.Add(new BasketLineVM
            {
                Kind = "Service",
                Title = ServiceName!.Trim(),
                Qty = qty,
                UnitPrice = price,
                Currency = string.IsNullOrWhiteSpace(ServiceCurrency) ? "EUR" : ServiceCurrency!
            });

            ServiceName = ""; ServiceQty = "1"; ServicePrice = "0"; ServiceCurrency = "EUR";
        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedLine == null) return;
            Basket.Remove(SelectedLine);
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (SelectedCustomer == null || string.IsNullOrWhiteSpace(Title) || ResFrom == null || ResTo == null) return;

            var r = new Reservation
            {
                CustomerId = SelectedCustomer.Id,
                Title = Title!.Trim(),
                StartDate = ResFrom!.Value.Date,
                EndDate = ResTo!.Value.Date,
                DepositDueDate = DepositDue,
                BalanceDueDate = BalanceDue,
                // Status = <omit>  <-- let default enum value apply
                Notes = Notes
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

            // reset form
            Title = "";
            Notes = "";
            Basket.Clear();
        }

        [RelayCommand]
        private void Cancel()
        {
            Basket.Clear();
            Title = ""; Notes = "";
        }
    }
}
