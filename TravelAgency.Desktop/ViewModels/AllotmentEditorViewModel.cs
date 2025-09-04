// Desktop/ViewModels/AllotmentEditorViewModel.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Enums;
using TravelAgency.Services;

// using TravelAgency.Domain.Entities; // αν θες map

namespace TravelAgency.Desktop.ViewModels
{
    public class AllotmentEditorViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly IAllotmentService _svc;
        private readonly Dictionary<string, List<string>> _errors = new();

        // Κλείσιμο παραθύρου από το VM
        public event Action<bool>? CloseRequested;

        private bool _isNew;
        private int? _allotmentId;
        private bool _isDirty;

        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public AllotmentEditorViewModel(
            IDbContextFactory<TravelAgencyDbContext> dbf,
            LookupCacheService cache,
            IAllotmentService svc)
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

            Cities = new ObservableCollection<CityVM>();
            Hotels = new ObservableCollection<HotelVM>();
            FilteredHotels = new ObservableCollection<HotelVM>();
            RoomTypes = new ObservableCollection<RoomTypeVM>();

            Lines = new ObservableCollection<AllotmentLineVM>();
            Payments = new ObservableCollection<PaymentVM>();
            History = new ObservableCollection<UpdateLogVM>();

            Lines.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
            Payments.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
        }

        #region Public bindable

        public string HeaderTitle => _isNew ? "New Allotment" : $"Edit Allotment #{_allotmentId}";

        public ObservableCollection<CityVM> Cities { get; }
        public ObservableCollection<HotelVM> Hotels { get; }
        public ObservableCollection<HotelVM> FilteredHotels { get; }  // based on SelectedCity
        public ObservableCollection<RoomTypeVM> RoomTypes { get; }

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

        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set { if (Set(ref _title, value)) { ValidateTitle(); MarkDirty(); } }
        }

        private DateTime? _startDate = DateTime.Today;

        public DateTime? StartDate
        {
            get => _startDate;
            set { if (Set(ref _startDate, value)) { ValidateDates(); RecalcTotals(); MarkDirty(); } }
        }

        private DateTime? _endDate = DateTime.Today.AddDays(1);

        public DateTime? EndDate
        {
            get => _endDate;
            set { if (Set(ref _endDate, value)) { ValidateDates(); RecalcTotals(); MarkDirty(); } }
        }

        private DateTime? _optionDueDate;

        public DateTime? OptionDueDate
        {
            get => _optionDueDate;
            set { if (Set(ref _optionDueDate, value)) MarkDirty(); }
        }

        private string _datePolicy = "ExactDates"; // binding μέσω ComboBoxItem Content

        public string DatePolicy
        {
            get => _datePolicy;
            set { if (Set(ref _datePolicy, value)) MarkDirty(); }
        }

        public ObservableCollection<AllotmentLineVM> Lines { get; }
        private AllotmentLineVM? _selectedLine;

        public AllotmentLineVM? SelectedLine
        {
            get => _selectedLine;
            set { if (Set(ref _selectedLine, value)) RaiseCanExec(); }
        }

        public ObservableCollection<PaymentVM> Payments { get; }
        private PaymentVM? _selectedPayment;

        public PaymentVM? SelectedPayment
        {
            get => _selectedPayment;
            set { if (Set(ref _selectedPayment, value)) RaiseCanExec(); }
        }

        public ObservableCollection<UpdateLogVM> History { get; }

        private int _nights;
        public int Nights { get => _nights; private set => Set(ref _nights, value); }

        private decimal _baseCost;
        public decimal BaseCost { get => _baseCost; private set => Set(ref _baseCost, value); }

        private decimal _paidTotal;
        public decimal PaidTotal { get => _paidTotal; private set => Set(ref _paidTotal, value); }

        private decimal _balance;
        public decimal Balance { get => _balance; private set => Set(ref _balance, value); }

        public bool CanSave => _isDirty && !HasErrors && SelectedHotel != null && SelectedCity != null && Lines.Any();

        public ICommand AddLineCommand { get; }
        public ICommand RemoveSelectedLineCommand { get; }
        public ICommand AddPaymentCommand { get; }
        public ICommand RemoveSelectedPaymentCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion Public bindable

        #region Init / Load

        public async Task InitializeAsNewAsync()
        {
            _isNew = true;
            _allotmentId = null;

            await LoadLookupsAsync();

            Title = string.Empty;
            SelectedCity = Cities.FirstOrDefault();
            ApplyHotelFilter();
            SelectedHotel = null;

            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddDays(1);
            OptionDueDate = null;
            DatePolicy = "ExactDates";

            Lines.Clear();
            Payments.Clear();
            History.Clear();

            _isDirty = false;
            RaiseAll();
        }

        public async Task InitializeForEditAsync(int id)
        {
            _isNew = false; _allotmentId = id;
            await LoadLookupsAsync();

            await using var db = await _dbf.CreateDbContextAsync();

            var a = await db.Allotments
                .Include(x => x.Hotel)
                .Include(x => x.RoomTypes)
                .Include(x => x.Payments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == id);

            Title = a.Title;
            SelectedCity = Cities.FirstOrDefault(c => c.Id == (a.Hotel?.CityId ?? 0));
            ApplyHotelFilter();
            SelectedHotel = FilteredHotels.FirstOrDefault(h => h.Id == a.HotelId);
            StartDate = a.StartDate.Date;
            EndDate = a.EndDate.Date;
            OptionDueDate = a.OptionDueDate;
            DatePolicy = a.DatePolicy == AllotmentDatePolicy.PartialAllowed ? "PartialAllowed" : "ExactDates";

            Lines.Clear();
            foreach (var l in a.RoomTypes)
            {
                var vm = new AllotmentLineVM
                {
                    RoomType = RoomTypes.FirstOrDefault(r => r.Id == l.RoomTypeId),
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Currency = l.Currency,
                    Notes = l.Notes
                };
                HookLine(vm);
                Lines.Add(vm);
            }

            Payments.Clear();
            foreach (var p in a.Payments.OrderBy(p => p.Date))
            {
                Payments.Add(new PaymentVM
                {
                    Date = p.Date,
                    Title = p.Title,
                    Kind = p.Kind.ToString(),
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided
                });
            }

            // Ιστορικό (αν έχεις UpdateLogs table)
            History.Clear();
            var logs = await db.UpdateLogs
                .Where(u => (u.EntityName == "Allotment" && u.EntityId == id)
                         || (u.EntityName == "AllotmentRoomType" && db.AllotmentRoomTypes.Any(x => x.AllotmentId == id && x.Id == u.EntityId))
                         || (u.EntityName == "AllotmentPayment" && db.AllotmentPayments.Any(x => x.AllotmentId == id && x.Id == u.EntityId)))
                .OrderByDescending(u => u.ChangedAt)
                .Take(200)
                .AsNoTracking()
                .ToListAsync();

            foreach (var h in logs)
            {
                History.Add(new UpdateLogVM
                {
                    ChangedAtUtc = h.ChangedAt,
                    ChangedBy = h.ChangedBy,
                    EntityType = h.EntityName,
                    PropertyName = h.PropertyName,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue
                });
            }

            _isDirty = false;
            RecalcTotals();
            RaiseAll();
        }


        private async Task LoadLookupsAsync()
        {
            Cities.Clear();
            Hotels.Clear();
            RoomTypes.Clear();

            var (cities, hotels, roomTypes) = await _svc.LoadLookupsAsync();

            foreach (var c in cities) Cities.Add(c);
            foreach (var h in hotels) Hotels.Add(h);
            foreach (var r in roomTypes) RoomTypes.Add(r);
        }

        #endregion Init / Load

        #region Commands logic

        private void AddLine()
        {
            var vm = new AllotmentLineVM
            {
                RoomType = RoomTypes.FirstOrDefault(),
                Quantity = 1,
                PricePerNight = 0m,
                Currency = "EUR",
                Notes = ""
            };
            HookLine(vm);
            Lines.Add(vm);
        }

        private void HookLine(AllotmentLineVM vm)
        {
            vm.PropertyChanged += (_, __) =>
            {
                RecalcTotals();
                MarkDirty();
                RaiseCanExec();
            };
        }

        private void RemoveSelectedLine()
        {
            if (SelectedLine == null) return;
            Lines.Remove(SelectedLine);
            RecalcTotals();
            MarkDirty();
            RaiseCanExec();
        }

        private void AddPayment()
        {
            Payments.Add(new PaymentVM
            {
                Date = DateTime.Today,
                Title = "Payment",
                Kind = "Deposit",
                Amount = 0m,
                Currency = "EUR",
                Notes = "",
                IsVoided = false
            });
            RecalcTotals();
            MarkDirty();
            RaiseCanExec();
        }

        private void RemoveSelectedPayment()
        {
            if (SelectedPayment == null) return;
            Payments.Remove(SelectedPayment);
            RecalcTotals();
            MarkDirty();
            RaiseCanExec();
        }

        private async Task SaveAsync()
        {
            ValidateTitle();
            ValidateDates();
            ValidateLines();
            if (!CanSave) return;

            await using var db = await _dbf.CreateDbContextAsync();

            // Map DatePolicy string -> enum
            var policy = DatePolicy == "PartialAllowed"
                ? AllotmentDatePolicy.PartialAllowed
                : AllotmentDatePolicy.ExactDates;

            if (_isNew)
            {
                var a = new TravelAgency.Domain.Entities.Allotment
                {
                    Title = Title.Trim(),
                    HotelId = SelectedHotel!.Id,
                    StartDate = StartDate!.Value.Date,
                    EndDate = EndDate!.Value.Date,
                    OptionDueDate = OptionDueDate,
                    DatePolicy = policy,
                    Status = AllotmentStatus.Active,
                    Notes = null
                };

                db.Allotments.Add(a);
                await db.SaveChangesAsync(); // για να πάρουμε Id

                // Lines
                foreach (var l in Lines)
                {
                    db.AllotmentRoomTypes.Add(new TravelAgency.Domain.Entities.AllotmentRoomType
                    {
                        AllotmentId = a.Id,
                        RoomTypeId = l.RoomType?.Id ?? 0,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = l.Currency,
                        Notes = l.Notes
                    });
                }

                // Payments
                foreach (var p in Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                    db.AllotmentPayments.Add(new TravelAgency.Domain.Entities.AllotmentPayment
                    {
                        AllotmentId = a.Id,
                        Date = p.Date,
                        Title = p.Title?.Trim() ?? "Payment",
                        Kind = kind,
                        Amount = p.Amount,
                        Currency = string.IsNullOrWhiteSpace(p.Currency) ? "EUR" : p.Currency!,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided
                    });
                }

                await db.SaveChangesAsync();
                _allotmentId = a.Id;
                _isNew = false;
            }
            else
            {
                // EDIT MODE
                var a = await db.Allotments
                    .Include(x => x.RoomTypes)
                    .Include(x => x.Payments)
                    .FirstAsync(x => x.Id == _allotmentId!.Value);

                a.Title = Title.Trim();
                a.HotelId = SelectedHotel!.Id;
                a.StartDate = StartDate!.Value.Date;
                a.EndDate = EndDate!.Value.Date;
                a.OptionDueDate = OptionDueDate;
                a.DatePolicy = policy;
                // a.Status      = a.Status;  // ή δώσε UI για αλλαγή status
                // a.Notes       = ...        // αν θες να δέσεις Notes στο UI

                // Replace Lines (MVP, καθαρό & ασφαλές)
                db.AllotmentRoomTypes.RemoveRange(a.RoomTypes);
                foreach (var l in Lines)
                {
                    db.AllotmentRoomTypes.Add(new TravelAgency.Domain.Entities.AllotmentRoomType
                    {
                        AllotmentId = a.Id,
                        RoomTypeId = l.RoomType?.Id ?? 0,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = l.Currency,
                        Notes = l.Notes
                    });
                }

                // Replace Payments (MVP)
                db.AllotmentPayments.RemoveRange(a.Payments);
                foreach (var p in Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                    db.AllotmentPayments.Add(new TravelAgency.Domain.Entities.AllotmentPayment
                    {
                        AllotmentId = a.Id,
                        Date = p.Date,
                        Title = p.Title?.Trim() ?? "Payment",
                        Kind = kind,
                        Amount = p.Amount,
                        Currency = string.IsNullOrWhiteSpace(p.Currency) ? "EUR" : p.Currency!,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided
                    });
                }

                await db.SaveChangesAsync();
            }

            // Προαιρετικό: ανανέωσε totals από το service σου (αν θέλεις live refresh στο VM)
            if (_allotmentId.HasValue)
            {
                var (baseCost, paid, balance) = await _svc.GetTotalsAsync(_allotmentId.Value);
                BaseCost = baseCost;
                PaidTotal = paid;
                Balance = balance;
            }
            else
            {
                RecalcTotals();
            }

            _isDirty = false;
            OnPropertyChanged(nameof(HeaderTitle));
            RaiseCanExec();

            // Κλείσε το dialog με OK
            CloseRequested?.Invoke(true);
        }


        #endregion Commands logic

        #region Helpers (calc, filter, dirty, validation)

        private void ApplyHotelFilter()
        {
            FilteredHotels.Clear();
            if (SelectedCity == null) return;
            foreach (var h in Hotels.Where(h => h.CityId == SelectedCity.Id))
                FilteredHotels.Add(h);
            if (!FilteredHotels.Contains(SelectedHotel!))
                SelectedHotel = null;
        }

        private void RecalcTotals()
        {
            var nights = 0;
            if (StartDate.HasValue && EndDate.HasValue)
            {
                var d = (EndDate.Value.Date - StartDate.Value.Date).Days;
                nights = Math.Max(0, d);
            }
            Nights = nights;

            decimal baseCost = 0m;
            foreach (var l in Lines)
                baseCost += l.PricePerNight * l.Quantity * nights;
            BaseCost = baseCost;

            decimal paid = 0m;
            foreach (var p in Payments)
                if (!p.IsVoided) paid += p.Amount;
            PaidTotal = paid;

            Balance = BaseCost - PaidTotal;

            foreach (var l in Lines)
                l.SetNightsForLineTotal(nights);
        }

        private void MarkDirty()
        {
            _isDirty = true;
            RaiseCanExec();
        }

        private void RaiseCanExec()
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveSelectedPaymentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(string.Empty);
            RaiseCanExec();
        }

        // Validation

        public bool HasErrors => _errors.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
            => propertyName != null && _errors.ContainsKey(propertyName) ? _errors[propertyName] : Enumerable.Empty<string>();

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

        private void ValidateTitle()
        {
            const string prop = nameof(Title);
            ClearErrors(prop);
            if (string.IsNullOrWhiteSpace(Title))
                SetError(prop, "Title is required.");
        }

        private void ValidateDates()
        {
            const string s = nameof(StartDate);
            const string e = nameof(EndDate);
            ClearErrors(s); ClearErrors(e);

            if (!StartDate.HasValue) SetError(s, "Start date is required.");
            if (!EndDate.HasValue) SetError(e, "End date is required.");
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
                SetError(e, "End date must be after or equal to Start date.");
        }

        private void ValidateLines()
        {
            const string prop = nameof(Lines);
            ClearErrors(prop);
            if (!Lines.Any()) { SetError(prop, "At least one room type line is required."); return; }
            if (Lines.Any(l => l.RoomType == null || l.Quantity <= 0 || l.PricePerNight < 0))
                SetError(prop, "Each line must have Room Type, Quantity > 0, and non-negative Price.");
        }

        #endregion Helpers (calc, filter, dirty, validation)

        #region INotifyPropertyChanged

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

        #endregion INotifyPropertyChanged
    }

    #region Child VMs & DTOs & Services (minimal)

    public class AllotmentLineVM : INotifyPropertyChanged
    {
        private RoomTypeVM? _roomType;
        private int _quantity;
        private decimal _pricePerNight;
        private string _currency = "EUR";
        private string? _notes;
        private int _nights;
        private decimal _lineTotal;

        public RoomTypeVM? RoomType
        { get => _roomType; set { if (Set(ref _roomType, value)) Recalc(); } }

        public int Quantity
        { get => _quantity; set { if (Set(ref _quantity, value)) Recalc(); } }

        public decimal PricePerNight
        { get => _pricePerNight; set { if (Set(ref _pricePerNight, value)) Recalc(); } }

        public string Currency
        { get => _currency; set { if (Set(ref _currency, value)) Recalc(); } }

        public string? Notes
        { get => _notes; set { Set(ref _notes, value); } }

        public decimal LineTotal { get => _lineTotal; private set => Set(ref _lineTotal, value); }

        public void SetNightsForLineTotal(int nights)
        { _nights = nights; Recalc(); }

        private void Recalc() => LineTotal = _nights * _pricePerNight * _quantity;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    public class PaymentVM : INotifyPropertyChanged
    {
        private DateTime _date = DateTime.Today;
        private string _title = "";
        private string _kind = "Deposit"; // string για απλότητα στο grid
        private decimal _amount;
        private string _currency = "EUR";
        private string? _notes;
        private bool _isVoided;

        public DateTime Date { get => _date; set => Set(ref _date, value); }
        public string Title { get => _title; set => Set(ref _title, value); }
        public string Kind { get => _kind; set => Set(ref _kind, value); }
        public decimal Amount { get => _amount; set => Set(ref _amount, value); }
        public string Currency { get => _currency; set => Set(ref _currency, value); }
        public string? Notes { get => _notes; set => Set(ref _notes, value); }
        public bool IsVoided { get => _isVoided; set => Set(ref _isVoided, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    public class UpdateLogVM
    {
        public DateTime ChangedAtUtc { get; set; }
        public string? ChangedBy { get; set; }
        public string EntityType { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public interface IWindowNavigator

    { void CloseWindow(); }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        private readonly Predicate<object?>? _can;

        public RelayCommand(Action<object?> exec, Predicate<object?>? can = null)
        { _exec = exec; _can = can; }

        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _exec(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion Child VMs & DTOs & Services (minimal)
}