// Desktop/ViewModels/ReservationEditorViewModel.cs
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Desktop.Helpers;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Enums;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public class ReservationEditorViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;
        private readonly IReservationService _svc;

        public event Action<bool>? CloseRequested;

        public ReservationEditorViewModel(
            IDbContextFactory<TravelAgencyDbContext> dbf,
            LookupCacheService cache,
            IReservationService svc)
        {
            _dbf = dbf;
            _cache = cache;
            _svc = svc;

            AddLineCommand = new RelayCommand(_ => AddLine());
            RemoveSelectedLineCommand = new RelayCommand(_ => RemoveSelectedLine(), _ => SelectedLine != null);
            AddPaymentCommand = new RelayCommand(_ => AddPayment());
            RemoveSelectedPaymentCommand = new RelayCommand(_ => RemoveSelectedPayment(), _ => SelectedPayment != null);
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

            SearchAvailabilityCommand = new RelayCommand(async _ => await SearchAvailabilityAsync(), CanSearch());
            AddOptionToReservationCommand = new RelayCommand(opt => AddOptionToReservation(opt as AvailableAllotmentVM));

            Customers = new ObservableCollection<CustomerVM>();
            Cities = new ObservableCollection<CityVM>();
            Hotels = new ObservableCollection<HotelVM>();
            FilteredHotels = new ObservableCollection<HotelVM>();

            Lines = new ObservableCollection<ReservationLineVM>();
            Payments = new ObservableCollection<PaymentVM>();
            History = new ObservableCollection<UpdateLogVM>();
            AvailableOptions = new ObservableCollection<AvailableAllotmentVM>();

            Lines.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
            Payments.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
        }

        private Predicate<object?> CanSearch()
        {
            return _ => SelectedHotel != null && CheckIn != null && CheckOut != null && CheckOut > CheckIn;
        }

        // ---------- Header ----------
        private bool _isNew;

        private int? _reservationId;
        public string HeaderTitle => _isNew ? "New Reservation" : $"Edit Reservation #{_reservationId}";

        // ---------- Lookups & filters ----------
        public ObservableCollection<CustomerVM> Customers { get; }

        public ObservableCollection<CityVM> Cities { get; }
        public ObservableCollection<HotelVM> Hotels { get; }
        public ObservableCollection<HotelVM> FilteredHotels { get; }

        private CustomerVM? _selectedCustomer;

        public CustomerVM? SelectedCustomer
        {
            get => _selectedCustomer;
            set { if (Set(ref _selectedCustomer, value)) { MarkDirty(); RaiseCanExec(); } }
        }

        public int CustomerId => _selectedCustomer?.Id ?? 0;

        private CityVM? _selectedCity;

        public CityVM? SelectedCity
        {
            get => _selectedCity;
            set { if (Set(ref _selectedCity, value)) { ApplyHotelFilter(); MarkDirty(); } }
        }

        private HotelVM? _selectedHotel;

        public HotelVM? SelectedHotel
        {
            get => _selectedHotel;
            set { if (Set(ref _selectedHotel, value)) MarkDirty(); }
        }

        // ---------- Dates & totals ----------
        private DateTime? _checkIn = DateTime.Today;

        public DateTime? CheckIn
        {
            get => _checkIn;
            set
            {
                if (Set(ref _checkIn, value))
                {
                    ValidateDates(); RecalcTotals(); MarkDirty(); UpdateNightsOnLines();
                }
            }
        }

        private DateTime? _checkOut = DateTime.Today.AddDays(1);

        public DateTime? CheckOut
        {
            get => _checkOut;
            set
            {
                if (Set(ref _checkOut, value))
                {
                    ValidateDates(); RecalcTotals(); MarkDirty(); UpdateNightsOnLines();
                }
            }
        }

        private void UpdateNightsOnLines()
        {
            var nights = Nights;
            foreach (var line in Lines)
                line.SetNightsForLineTotal(nights);
        }

        private int _nights;
        public int Nights { get => _nights; private set => Set(ref _nights, value); }

        private decimal _total;
        public decimal Total { get => _total; private set => Set(ref _total, value); }

        private decimal _paid;
        public decimal PaidTotal { get => _paid; private set => Set(ref _paid, value); }

        private decimal _balance;
        public decimal Balance { get => _balance; private set => Set(ref _balance, value); }

        // ---------- Lines / Payments / History ----------
        public ObservableCollection<ReservationLineVM> Lines { get; }

        private ReservationLineVM? _selectedLine;

        public ReservationLineVM? SelectedLine
        { get => _selectedLine; set { if (Set(ref _selectedLine, value)) RaiseCanExec(); } }

        public ObservableCollection<PaymentVM> Payments { get; }
        private PaymentVM? _selectedPayment;

        public PaymentVM? SelectedPayment
        { get => _selectedPayment; set { if (Set(ref _selectedPayment, value)) RaiseCanExec(); } }

        public ObservableCollection<UpdateLogVM> History { get; }

        // ---------- Availability ----------
        public ObservableCollection<AvailableAllotmentVM> AvailableOptions { get; }

        private AvailableAllotmentVM? _selectedAvailableOption;
        public AvailableAllotmentVM? SelectedAvailableOption { get => _selectedAvailableOption; set => Set(ref _selectedAvailableOption, value); }

        // ---------- State / Commands ----------
        private bool _dirty;

        public bool CanSave => _dirty && !HasErrors && CustomerId > 0 && Lines.Any();

        public ICommand AddLineCommand { get; }
        public ICommand RemoveSelectedLineCommand { get; }
        public ICommand AddPaymentCommand { get; }
        public ICommand RemoveSelectedPaymentCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand SearchAvailabilityCommand { get; }
        public ICommand AddOptionToReservationCommand { get; }

        public async Task InitializeAsNewAsync()
        {
            using (Busy.Begin())
            {
                _isNew = true; _reservationId = null;

                await LoadLookupsAsync();

                ApplyHotelFilter();
                SelectedHotel = null;

                CheckIn = DateTime.Today;
                CheckOut = DateTime.Today.AddDays(1);

                Lines.Clear();
                Payments.Clear();
                History.Clear();
                AvailableOptions.Clear();

                _dirty = false;
                RecalcTotals();
                RaiseAll();
            }
        }


        public async Task InitializeAsNewForCustomerAsync()
        {
            using (Busy.Begin())
            {
                await InitializeAsNewAsync();
            }
        }


        public async Task InitializeForEditAsync(int id)
        {
            using (Busy.Begin())
            {
                _isNew = false; _reservationId = id;

                await LoadLookupsAsync();

                var dto = await _svc.LoadAsync(id); // already inside Busy scope
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == dto.CustomerId);

                SelectedCity = null; ApplyHotelFilter(); SelectedHotel = null;

                CheckIn = dto.CheckInUtc.ToLocalTime().Date;
                CheckOut = dto.CheckOutUtc.ToLocalTime().Date;

                Lines.Clear();
                foreach (var l in dto.Lines)
                {
                    var vm = new ReservationLineVM
                    {
                        Id = l.Id,
                        AllotmentRoomTypeId = l.AllotmentRoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Notes = l.Notes,
                    };
                    HookLine(vm);
                    Lines.Add(vm);
                }

                Payments.Clear();
                foreach (var p in dto.Payments.OrderBy(p => p.DateUtc))
                {
                    var pvm = new PaymentVM
                    {
                        Id = p.Id,
                        Date = p.DateUtc.ToLocalTime().Date,
                        Title = p.Title,
                        Kind = p.Kind,
                        Amount = p.Amount,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided,
                        UpdatedAtLocal = p.UpdatedAtUtc?.ToLocalTime()
                    };
                    HookPayment(pvm);
                    Payments.Add(pvm);
                }

                History.Clear();
                foreach (var h in dto.History.OrderByDescending(x => x.ChangedAtUtc))
                {
                    History.Add(new UpdateLogVM
                    {
                        ChangedAtUtc = h.ChangedAtUtc,
                        ChangedBy = h.ChangedBy,
                        EntityName = h.EntityName,
                        PropertyName = h.PropertyName,
                        OldValue = h.OldValue,
                        NewValue = h.NewValue
                    });
                }

                _dirty = false;
                RecalcTotals();
                RaiseAll();
            }
        }


        private async Task LoadLookupsAsync()
        {
            using (Busy.Begin())
            {
                await _cache.WarmUpAsync();

                Customers.Clear();
                foreach (var c in _cache.Customers.Select(c => new CustomerVM { Id = c.Id, Name = c.Name }))
                    Customers.Add(c);

                Cities.Clear();
                foreach (var ci in _cache.Cities.Select(ci => new CityVM { Id = ci.Id, Name = ci.Name }))
                    Cities.Add(ci);

                Hotels.Clear();
                foreach (var h in _cache.Hotels.Select(h => new HotelVM { Id = h.Id, Name = h.Name, CityId = h.CityId }))
                    Hotels.Add(h);
            }
        }


        private void ApplyHotelFilter()
        {
            FilteredHotels.Clear();
            if (SelectedCity == null) return;

            foreach (var h in Hotels.Where(h => h.CityId == SelectedCity.Id))
                FilteredHotels.Add(h);

            if (SelectedHotel != null && !FilteredHotels.Contains(SelectedHotel))
                SelectedHotel = null;
        }

        // =========================================
        // Availability search (EF in VM for now)
        // =========================================
        private async Task SearchAvailabilityAsync()
        {
            if (SelectedHotel == null || CheckIn == null || CheckOut == null || CheckOut <= CheckIn) return;

            using (Busy.Begin())
            {
                var start = CheckIn.Value.Date;
                var endEx = CheckOut.Value.Date; // exclusive

                AvailableOptions.Clear();

                await using var db = await _dbf.CreateDbContextAsync();

                var arts = await db.AllotmentRoomTypes
                    .Include(rt => rt.Allotment)!.ThenInclude(a => a.Hotel)
                    .Include(rt => rt.RoomType)
                    .Where(rt =>
                        rt.Allotment!.HotelId == SelectedHotel.Id &&
                        rt.Allotment.StartDate < endEx &&
                        rt.Allotment.EndDate > start)
                    .AsNoTracking()
                    .ToListAsync();

                if (arts.Count == 0) return;

                var artIds = arts.Select(x => x.Id).ToList();

                var resLines = await db.ReservationLines
                    .Include(rl => rl.Reservation)
                    .Where(rl =>
                        artIds.Contains(rl.AllotmentRoomTypeId) &&
                        rl.Reservation!.Status != ReservationStatus.Cancelled &&
                        rl.Reservation.CheckIn < endEx &&
                        rl.Reservation.CheckOut > start &&
                        (_reservationId == null || rl.ReservationId != _reservationId.Value))
                    .AsNoTracking()
                    .ToListAsync();

                var reservedByDay = new Dictionary<(int artId, DateTime day), int>();
                foreach (var rl in resLines)
                {
                    var s = rl.Reservation!.CheckIn < start ? start : rl.Reservation.CheckIn;
                    var e = rl.Reservation.CheckOut > endEx ? endEx : rl.Reservation.CheckOut;

                    for (var d = s; d < e; d = d.AddDays(1))
                    {
                        var key = (rl.AllotmentRoomTypeId, d);
                        reservedByDay[key] = reservedByDay.TryGetValue(key, out var cur) ? cur + rl.Quantity : rl.Quantity;
                    }
                }

                foreach (var art in arts)
                {
                    int minFree = int.MaxValue;

                    var s = art.Allotment!.StartDate > start ? art.Allotment.StartDate.Date : start;
                    var e = art.Allotment.EndDate < endEx ? art.Allotment.EndDate.Date : endEx;
                    if (s >= e) continue;

                    for (var d = s; d < e; d = d.AddDays(1))
                    {
                        reservedByDay.TryGetValue((art.Id, d), out var reservedQty);
                        var free = Math.Max(0, art.Quantity - reservedQty);
                        if (free < minFree) minFree = free;
                    }

                    if (minFree == int.MaxValue) minFree = art.Quantity;

                    var display = $"{art.Allotment!.Hotel!.Name} · {art.RoomType!.Name} (Free {minFree}/{art.Quantity}) – {art.PricePerNight:0.##} €";

                    AvailableOptions.Add(new AvailableAllotmentVM
                    {
                        AllotmentRoomTypeId = art.Id,
                        Display = display,
                        HotelName = art.Allotment.Hotel!.Name,
                        RoomTypeName = art.RoomType.Name,
                        PricePerNight = art.PricePerNight,
                        TotalCapacity = art.Quantity,
                        FreeMinOverNights = minFree
                    });
                }
            }
        }


        private void AddOptionToReservation(AvailableAllotmentVM? opt)
        {
            if (opt == null) return;
            if (opt.FreeMinOverNights <= 0) return;

            var vm = new ReservationLineVM
            {
                AllotmentRoomTypeId = opt.AllotmentRoomTypeId,
                Quantity = 1,
                PricePerNight = opt.PricePerNight,
                Notes = "",
                // Useful to show in the grid
                Display = opt.Display
            };
            HookLine(vm);
            Lines.Add(vm);
        }

        // =========================================
        // Commands logic (lines / payments / save)
        // =========================================
        private void AddLine()
        {
            // Fallback: add an empty line (user should pick from availability)
            var vm = new ReservationLineVM
            {
                AllotmentRoomTypeId = 0,
                Quantity = 1,
                PricePerNight = 0m,
                Notes = ""
            };
            HookLine(vm);
            Lines.Add(vm);
        }

        private void HookLine(ReservationLineVM vm)
            => vm.PropertyChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };

        private void RemoveSelectedLine()
        {
            if (SelectedLine == null) return;
            Lines.Remove(SelectedLine);
            RecalcTotals(); MarkDirty(); RaiseCanExec();
        }

        private void AddPayment()
        {
            var vm = new PaymentVM
            {
                Date = DateTime.Today,
                Title = "Payment",
                Kind = "Deposit",
                Amount = 0m,
                Notes = "",
                IsVoided = false
            };
            HookPayment(vm);
            Payments.Add(vm);
        }

        private void HookPayment(PaymentVM vm)
            => vm.PropertyChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };

        private void RemoveSelectedPayment()
        {
            if (SelectedPayment == null) return;
            Payments.Remove(SelectedPayment);
            RecalcTotals(); MarkDirty(); RaiseCanExec();
        }

        private async Task SaveAsync()
        {
            ValidateDates();
            ValidateLines();
            if (!CanSave) return;

            var dto = new ReservationDto
            {
                Id = _reservationId,
                CustomerId = CustomerId,
                CheckInUtc = (CheckIn ?? DateTime.Today).ToUniversalTime(),
                CheckOutUtc = (CheckOut ?? DateTime.Today).ToUniversalTime(),
                Lines = Lines.Select(l => new ReservationLineDto
                {
                    Id = l.Id,
                    AllotmentRoomTypeId = l.AllotmentRoomTypeId,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Notes = l.Notes
                }).ToList(),
                Payments = Payments.Select(p => new PaymentDto
                {
                    Id = p.Id,
                    DateUtc = p.Date.ToUniversalTime(),
                    Title = p.Title,
                    Kind = p.Kind,
                    Amount = p.Amount,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided,
                    UpdatedAtUtc = p.UpdatedAtLocal?.ToUniversalTime()
                }).ToList()
            };

            var result = await Busy.RunAsync(() => _svc.SaveAsync(dto));
            if (!result.Success) return;

            _reservationId = result.Id;
            _isNew = false;

            _dirty = false;
            OnPropertyChanged(nameof(HeaderTitle));
            RaiseCanExec();

            CloseRequested?.Invoke(true);
        }

        // =========================================
        // Validation
        // =========================================
        private readonly Dictionary<string, List<string>> _errors = new();

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName)
            => propertyName != null && _errors.TryGetValue(propertyName, out var list) ? list : Enumerable.Empty<string>();

        private void SetError(string prop, string message)
        {
            if (!_errors.TryGetValue(prop, out var list))
            {
                list = new List<string>();
                _errors[prop] = list;
            }
            if (!list.Contains(message))
                list.Add(message);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(prop));
            OnPropertyChanged(nameof(CanSave));
        }

        private void ClearErrors(string prop)
        {
            if (_errors.Remove(prop))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(prop));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        private void ValidateDates()
        {
            const string s = nameof(CheckIn);
            const string e = nameof(CheckOut);
            ClearErrors(s); ClearErrors(e);

            if (!CheckIn.HasValue) SetError(s, "Check-in is required.");
            if (!CheckOut.HasValue) SetError(e, "Check-out is required.");
            if (CheckIn.HasValue && CheckOut.HasValue && CheckOut.Value.Date <= CheckIn.Value.Date)
                SetError(e, "Check-out must be after Check-in.");
        }

        private void ValidateLines()
        {
            const string prop = nameof(Lines);
            ClearErrors(prop);
            if (!Lines.Any()) { SetError(prop, "At least one line is required."); return; }
            if (Lines.Any(l => l.AllotmentRoomTypeId <= 0 || l.Quantity <= 0 || l.PricePerNight < 0))
                SetError(prop, "Each line must have Allotment/RoomType, Quantity > 0 and non-negative Price.");
        }

        // =========================================
        // Totals & helpers
        // =========================================
        private void RecalcTotals()
        {
            var nights = 0;
            if (CheckIn.HasValue && CheckOut.HasValue)
            {
                var d = (CheckOut.Value.Date - CheckIn.Value.Date).Days;
                nights = Math.Max(0, d);
            }
            Nights = nights;

            decimal total = 0m;
            foreach (var l in Lines)
                total += l.PricePerNight * l.Quantity * nights;
            Total = total;

            decimal paid = 0m;
            foreach (var p in Payments)
                if (!p.IsVoided) paid += p.Amount;
            PaidTotal = paid;

            Balance = Total - PaidTotal;

            foreach (var l in Lines)
                l.SetNightsForLineTotal(nights);
        }

        private void MarkDirty()
        { _dirty = true; RaiseCanExec(); }

        private void RaiseCanExec()
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SearchAvailabilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedPaymentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(string.Empty);
            RaiseCanExec();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AvailableAllotmentVM
    {
        public int AllotmentRoomTypeId { get; init; }
        public string Display { get; init; } = "";
        public string HotelName { get; init; } = "";
        public string RoomTypeName { get; init; } = "";
        public decimal PricePerNight { get; init; }
        public int TotalCapacity { get; init; }
        public int FreeMinOverNights { get; init; }
    }
}