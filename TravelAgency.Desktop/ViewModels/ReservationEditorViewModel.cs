using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Domain.Models;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class ReservationEditorViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;
        private readonly int _customerId;
        private readonly int? _reservationId;
        // Filtered views
        private readonly ListCollectionView _roomsView;
        private readonly ListCollectionView _servicesView;

        public ICollectionView RoomsView => _roomsView;
        public ICollectionView ServicesView => _servicesView;
        [ObservableProperty] private ReservationBasketLine? selectedBasketLine;

        public ObservableCollection<City> Cities => _cache.Cities;

        [ObservableProperty] private string? title;
        [ObservableProperty] private DateTime? from = DateTime.Today;
        [ObservableProperty] private DateTime? to = DateTime.Today.AddDays(3);

        [ObservableProperty] private DateTime? depositDueDate;
        [ObservableProperty] private DateTime? balanceDueDate;

        // Availability (grouped by allotment)
        public ObservableCollection<AvailableAllotmentDto> Available { get; } = new();
        [ObservableProperty] private AvailableAllotmentDto? selectedAvailable;

        // Per-allotment candidates (room types)
        public ObservableCollection<AddLineCandidate> AddLineCandidates { get; } = new();
        [ObservableProperty] private AddLineCandidate? selectedAddLine;
        [ObservableProperty] private string? addQty = "1";

        // Basket (rooms + services)
        public ObservableCollection<ReservationBasketLine> Basket { get; } = new();

        [ObservableProperty] private City? selectedCity;

        // Service subform
        [ObservableProperty] private string? serviceName;
        [ObservableProperty] private string? serviceQty = "1";
        [ObservableProperty] private string? servicePrice = "0";

        public decimal GrandTotal => Basket.Sum(b => b.LineTotal);

        public ReservationEditorViewModel(
            IDbContextFactory<TravelAgencyDbContext> dbf,
            LookupCacheService cache,
            int customerId,
            int? reservationId = null)
        {
            _dbf = dbf;
            _cache = cache;
            _customerId = customerId;
            _reservationId = reservationId;

            // two independent views over the same ObservableCollection
            _roomsView = new ListCollectionView(Basket);
            _servicesView = new ListCollectionView(Basket);

            _roomsView.Filter = o => o is ReservationBasketLine l && l.Kind == "AllotmentRoom";
            _servicesView.Filter = o => o is ReservationBasketLine l && l.Kind == "Service";

            // keep views & total fresh
            Basket.CollectionChanged += (_, __) =>
            {
                _roomsView.Refresh();
                _servicesView.Refresh();
                OnPropertyChanged(nameof(GrandTotal));
            };

        }

        // -------- lifecycle --------

        public async Task InitializeAsync()
        {
            if (_reservationId.HasValue)
                await LoadExistingAsync(_reservationId.Value);

            await LoadAvailabilityAsync();
            await LoadCandidatesAsync();
            OnPropertyChanged(nameof(GrandTotal));
        }

        private async Task LoadExistingAsync(int id)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var r = await db.Reservations
                .Include(x => x.Items)
                .FirstAsync(x => x.Id == id);

            Title = r.Title;
            From = r.StartDate;
            To = r.EndDate;
            DepositDueDate = r.DepositDueDate;
            BalanceDueDate = r.BalanceDueDate;

            Basket.Clear();
            foreach (var it in r.Items)
            {
                if (it.Kind == ReservationItemKind.AllotmentRoom)
                {
                    Basket.Add(new ReservationBasketLine
                    {
                        Kind = "AllotmentRoom",
                        AllotmentRoomTypeId = it.AllotmentRoomTypeId,
                        Title = "(existing room line)",
                        Qty = it.Qty,
                        UnitPrice = it.UnitPrice,
                        Currency = it.Currency
                    });
                }
                else
                {
                    Basket.Add(new ReservationBasketLine
                    {
                        Kind = "Service",
                        Title = it.ServiceName ?? "",
                        Qty = it.Qty,
                        UnitPrice = it.UnitPrice,
                        Currency = it.Currency
                    });
                }
            }
        }

        // Auto-refresh on filter changes
        partial void OnSelectedCityChanged(City? value) => _ = LoadAvailabilityAsync();
        partial void OnFromChanged(DateTime? value) => _ = LoadAvailabilityAsync();
        partial void OnToChanged(DateTime? value) => _ = LoadAvailabilityAsync();
        partial void OnSelectedAvailableChanged(AvailableAllotmentDto? value) => _ = LoadCandidatesAsync();

        // -------- availability & candidates --------

        [RelayCommand]
        private async Task LoadAvailabilityAsync()
        {
            Available.Clear();

            await using var db = await _dbf.CreateDbContextAsync();

            var lines = db.AllotmentRoomTypes
                .Include(l => l.RoomType)
                .Include(l => l.Allotment)!.ThenInclude(a => a.Hotel)!.ThenInclude(h => h.City)
                .AsQueryable();

            if (From.HasValue && To.HasValue)
                lines = lines.Where(l => l.Allotment!.EndDate >= From!.Value &&
                                         l.Allotment!.StartDate <= To!.Value);
            if (SelectedCity != null)
                lines = lines.Where(l => l.Allotment!.Hotel!.CityId == SelectedCity.Id);

            var lineWithSold =
                from l in lines
                join ri in db.ReservationItems.Include(x => x.Reservation)
                    on l.Id equals ri.AllotmentRoomTypeId into g
                select new
                {
                    Line = l,
                    Sold = g.Where(x => x.Reservation.Status != ReservationStatus.Cancelled)
                            .Sum(x => (int?)x.Qty) ?? 0
                };

            var availableLines = await lineWithSold
    .Where(x => (x.Line.Quantity - x.Sold) > 0)
    .AsNoTracking()
    .ToListAsync();

            // Group strictly by AllotmentId to enforce uniqueness
            var grouped = availableLines
                .GroupBy(x => x.Line.AllotmentId)
                .Select(g =>
                {
                    var any = g.First().Line; // safe: group has at least one
                    return new AvailableAllotmentDto
                    {
                        AllotmentId = any.AllotmentId,
                        Title = any.Allotment!.Title,
                        HotelName = any.Allotment!.Hotel!.Name,
                        StartDate = any.Allotment!.StartDate,
                        EndDate = any.Allotment!.EndDate,
                        RemainingTotal = g.Sum(x => x.Line.Quantity - x.Sold)
                    };
                })
                .OrderBy(x => x.StartDate)
                .ToList();

            Available.Clear();
            foreach (var it in grouped) Available.Add(it);

            // refresh candidates list if a row is still selected
            await LoadCandidatesAsync();
        }

        [RelayCommand]
        private async Task LoadCandidatesAsync()
        {
            AddLineCandidates.Clear();
            if (SelectedAvailable == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

            var lines = await db.AllotmentRoomTypes
                .Include(l => l.RoomType)
                .Where(l => l.AllotmentId == SelectedAvailable.AllotmentId)
                .AsNoTracking()
                .ToListAsync();

            var lineIds = lines.Select(l => l.Id).ToList();

            var soldByLine = await db.ReservationItems
                .Include(ri => ri.Reservation)
                .Where(ri => ri.AllotmentRoomTypeId != null &&
                             lineIds.Contains(ri.AllotmentRoomTypeId.Value) &&
                             ri.Reservation.Status != ReservationStatus.Cancelled)
                .GroupBy(ri => ri.AllotmentRoomTypeId!.Value)
                .Select(g => new { LineId = g.Key, Qty = g.Sum(x => x.Qty) })
                .ToDictionaryAsync(x => x.LineId, x => x.Qty);

            foreach (var l in lines)
            {
                var sold = soldByLine.TryGetValue(l.Id, out var s) ? s : 0;
                var remaining = Math.Max(0, l.Quantity - sold);
                if (remaining <= 0) continue;

                AddLineCandidates.Add(new AddLineCandidate
                {
                    AllotmentRoomTypeId = l.Id,
                    Display = $"{l.RoomType!.Name} · Rem {remaining} @ {l.PricePerNight:0.##} {l.Currency}",
                    Remaining = remaining,
                    UnitPrice = l.PricePerNight,
                    Currency = l.Currency
                });
            }
        }

        // -------- basket actions --------

        [RelayCommand]
        private void AddAllotmentLine()
        {
            if (SelectedAddLine == null) return;
            if (!int.TryParse(AddQty ?? "1", out var qty) || qty <= 0) qty = 1;
            if (qty > SelectedAddLine.Remaining) qty = SelectedAddLine.Remaining;

            Basket.Add(new ReservationBasketLine
            {
                Kind = "AllotmentRoom",
                AllotmentRoomTypeId = SelectedAddLine.AllotmentRoomTypeId,
                Title = SelectedAddLine.Display,
                Qty = qty,
                UnitPrice = SelectedAddLine.UnitPrice,
                Currency = SelectedAddLine.Currency
            });

            SelectedAddLine = null;
            AddQty = "1";
            OnPropertyChanged(nameof(GrandTotal));
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
                Currency = "EUR"
            });

            ServiceName = "";
            ServiceQty = "1";
            ServicePrice = "0";
            OnPropertyChanged(nameof(GrandTotal));
        }

        [RelayCommand]
        private void RemoveBasketLine(ReservationBasketLine? line)
        {
            if (line == null) return;
            Basket.Remove(line);
            OnPropertyChanged(nameof(GrandTotal));
        }

        // -------- save / cancel --------

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Title) || !From.HasValue || !To.HasValue) return;

            await using var db = await _dbf.CreateDbContextAsync();

            Reservation r;
            if (_reservationId.HasValue)
            {
                r = await db.Reservations.Include(x => x.Items).FirstAsync(x => x.Id == _reservationId.Value);

                r.Title = Title!.Trim();
                r.StartDate = From.Value.Date;
                r.EndDate = To.Value.Date;
                r.DepositDueDate = DepositDueDate;
                r.BalanceDueDate = BalanceDueDate;

                // Replace items (simple MVP flow)
                db.ReservationItems.RemoveRange(r.Items);
            }
            else
            {
                r = new Reservation
                {
                    CustomerId = _customerId,
                    Title = Title!.Trim(),
                    StartDate = From.Value.Date,
                    EndDate = To.Value.Date,
                    DepositDueDate = DepositDueDate,
                    BalanceDueDate = BalanceDueDate,
                    Status = ReservationStatus.Draft
                };
                db.Reservations.Add(r);
                await db.SaveChangesAsync();
            }

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
            CloseWindow();
        }

        [RelayCommand]
        private void Cancel() => CloseWindow();

        private void CloseWindow()
        {
            foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
                if (w.DataContext == this) { w.Close(); break; }
        }

        // -------- helper row for candidate ComboBox --------
        public class AddLineCandidate
        {
            public int AllotmentRoomTypeId { get; set; }
            public string Display { get; set; } = "";
            public int Remaining { get; set; }
            public decimal UnitPrice { get; set; }
            public string Currency { get; set; } = "EUR";
        }
    }
}
