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
using TravelAgency.Domain.Enums;
// using TravelAgency.Domain.Entities; // αν θες map

namespace TravelAgency.Desktop.ViewModels
{
    public class AllotmentEditorViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly IAllotmentService _svc;           // inject
        private readonly IWindowNavigator _nav;            // προαιρετικό για Close
        private readonly Dictionary<string, List<string>> _errors = new();

        private bool _isNew;
        private int? _allotmentId;
        private bool _isDirty;

        public AllotmentEditorViewModel()
        {
            // TODO: inject με DI container – για τώρα dummy services
            _svc = new DummyAllotmentService();
            _nav = new DummyWindowNavigator();

            AddLineCommand = new RelayCommand(_ => AddLine());
            RemoveSelectedLineCommand = new RelayCommand(_ => RemoveSelectedLine(), _ => SelectedLine != null);
            AddPaymentCommand = new RelayCommand(_ => AddPayment());
            RemoveSelectedPaymentCommand = new RelayCommand(_ => RemoveSelectedPayment(), _ => SelectedPayment != null);
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave);
            CancelCommand = new RelayCommand(_ => _nav.CloseWindow());

            // lookups
            Cities = new ObservableCollection<CityVM>();
            Hotels = new ObservableCollection<HotelVM>();
            FilteredHotels = new ObservableCollection<HotelVM>();
            RoomTypes = new ObservableCollection<RoomTypeVM>();

            Lines = new ObservableCollection<AllotmentLineVM>();
            Payments = new ObservableCollection<PaymentVM>();
            History = new ObservableCollection<UpdateLogVM>();

            Lines.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
            Payments.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };

            // initial load as "new"
            _ = InitializeAsNewAsync();
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

        #endregion

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
            _isNew = false;
            _allotmentId = id;

            await LoadLookupsAsync();

            var dto = await _svc.LoadAsync(id);

            Title = dto.Title;
            SelectedCity = Cities.FirstOrDefault(c => c.Id == dto.CityId);
            ApplyHotelFilter();
            SelectedHotel = FilteredHotels.FirstOrDefault(h => h.Id == dto.HotelId);

            StartDate = dto.StartDateUtc.ToLocalTime().Date;
            EndDate = dto.EndDateUtc.ToLocalTime().Date;
            OptionDueDate = dto.OptionDueUtc?.ToLocalTime().Date;
            DatePolicy = dto.DatePolicy; // "ExactDates" / "PartialAllowed"

            Lines.Clear();
            foreach (var l in dto.Lines)
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
            foreach (var p in dto.Payments)
            {
                Payments.Add(new PaymentVM
                {
                    Date = p.DateUtc.ToLocalTime().Date,
                    Title = p.Title,
                    Kind = p.Kind,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided
                });
            }

            History.Clear();
            foreach (var h in dto.History)
            {
                History.Add(new UpdateLogVM
                {
                    ChangedAtUtc = h.ChangedAtUtc,
                    ChangedBy = h.ChangedBy,
                    EntityType = h.EntityType,
                    PropertyName = h.Property,
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

        #endregion

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

            var dto = new AllotmentDto
            {
                Id = _allotmentId,
                Title = Title,
                CityId = SelectedCity!.Id,
                HotelId = SelectedHotel!.Id,
                StartDateUtc = (StartDate ?? DateTime.Today).ToUniversalTime(),
                EndDateUtc = (EndDate ?? DateTime.Today).ToUniversalTime(),
                OptionDueUtc = OptionDueDate?.ToUniversalTime(),
                DatePolicy = DatePolicy,
                Lines = Lines.Select(l => new AllotmentLineDto
                {
                    RoomTypeId = l.RoomType?.Id ?? 0,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Currency = l.Currency,
                    Notes = l.Notes
                }).ToList(),
                Payments = Payments.Select(p => new PaymentDto
                {
                    DateUtc = p.Date.ToUniversalTime(),
                    Title = p.Title,
                    Kind = p.Kind,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided
                }).ToList()
            };

            var result = await _svc.SaveAsync(dto);
            if (result.Success)
            {
                _isDirty = false;
                _allotmentId = result.Id;
                _isNew = false;
                OnPropertyChanged(nameof(HeaderTitle));
                RaiseCanExec();
                // refresh history
                History.Clear();
                foreach (var h in result.History)
                {
                    History.Add(new UpdateLogVM
                    {
                        ChangedAtUtc = h.ChangedAtUtc,
                        ChangedBy = h.ChangedBy,
                        EntityType = h.EntityType,
                        PropertyName = h.Property,
                        OldValue = h.OldValue,
                        NewValue = h.NewValue
                    });
                }
            }
        }

        #endregion

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

        #endregion

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

        #endregion
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

        public RoomTypeVM? RoomType { get => _roomType; set { if (Set(ref _roomType, value)) Recalc(); } }
        public int Quantity { get => _quantity; set { if (Set(ref _quantity, value)) Recalc(); } }
        public decimal PricePerNight { get => _pricePerNight; set { if (Set(ref _pricePerNight, value)) Recalc(); } }
        public string Currency { get => _currency; set { if (Set(ref _currency, value)) Recalc(); } }
        public string? Notes { get => _notes; set { Set(ref _notes, value); } }

        public decimal LineTotal { get => _lineTotal; private set => Set(ref _lineTotal, value); }

        public void SetNightsForLineTotal(int nights) { _nights = nights; Recalc(); }

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

    public class CityVM { public int Id { get; set; } public string Name { get; set; } = ""; }
    public class HotelVM { public int Id { get; set; } public string Name { get; set; } = ""; public int CityId { get; set; } }
    public class RoomTypeVM { public int Id { get; set; } public string Name { get; set; } = ""; }

    public class AllotmentDto
    {
        public int? Id { get; set; }
        public string Title { get; set; } = "";
        public int CityId { get; set; }
        public int HotelId { get; set; }
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }
        public DateTime? OptionDueUtc { get; set; }
        public string DatePolicy { get; set; } = "ExactDates";
        public List<AllotmentLineDto> Lines { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public List<HistoryDto> History { get; set; } = new();
    }

    public class AllotmentLineDto
    {
        public int RoomTypeId { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerNight { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? Notes { get; set; }
    }

    public class PaymentDto
    {
        public DateTime DateUtc { get; set; }
        public string Title { get; set; } = "";
        public string Kind { get; set; } = "Deposit";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? Notes { get; set; }
        public bool IsVoided { get; set; }
    }

    public class HistoryDto
    {
        public DateTime ChangedAtUtc { get; set; }
        public string? ChangedBy { get; set; }
        public string EntityType { get; set; } = "";
        public string Property { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public interface IAllotmentService
    {
        Task<(IEnumerable<CityVM> cities, IEnumerable<HotelVM> hotels, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync();
        Task<AllotmentDto> LoadAsync(int id);
        Task<SaveResult> SaveAsync(AllotmentDto dto);
    }

    public class SaveResult
    {
        public bool Success { get; set; }
        public int Id { get; set; }
        public List<HistoryDto> History { get; set; } = new();
    }

    // Dummy implementations (plug your EF/Repo)
    public class DummyAllotmentService : IAllotmentService
    {
        public Task<(IEnumerable<CityVM>, IEnumerable<HotelVM>, IEnumerable<RoomTypeVM>)> LoadLookupsAsync()
        {
            var cities = new[] { new CityVM { Id = 1, Name = "Athens" }, new CityVM { Id = 2, Name = "Thessaloniki" } };
            var hotels = new[]
            {
                new HotelVM{ Id=10, Name="Athens Center Hotel", CityId=1 },
                new HotelVM{ Id=11, Name="Acropolis View", CityId=1 },
                new HotelVM{ Id=20, Name="Thess Panorama", CityId=2 }
            };
            var roomTypes = new[]
            {
                new RoomTypeVM{ Id=100, Name="Single"},
                new RoomTypeVM{ Id=101, Name="Double"},
                new RoomTypeVM{ Id=102, Name="Suite"}
            };
            return Task.FromResult((cities.AsEnumerable(), hotels.AsEnumerable(), roomTypes.AsEnumerable()));
        }

        public Task<AllotmentDto> LoadAsync(int id)
        {
            // demo data
            var dto = new AllotmentDto
            {
                Id = id,
                Title = "Sample Allotment",
                CityId = 1,
                HotelId = 10,
                StartDateUtc = DateTime.UtcNow.Date,
                EndDateUtc = DateTime.UtcNow.Date.AddDays(5),
                DatePolicy = "ExactDates",
                Lines = new List<AllotmentLineDto>
                {
                    new AllotmentLineDto{ RoomTypeId=101, Quantity=5, PricePerNight=120, Currency="EUR", Notes="" }
                },
                Payments = new List<PaymentDto>
                {
                    new PaymentDto{ DateUtc=DateTime.UtcNow.Date, Title="Deposit", Kind="Deposit", Amount=200m, Currency="EUR" }
                },
                History = new List<HistoryDto>()
            };
            return Task.FromResult(dto);
        }

        public Task<SaveResult> SaveAsync(AllotmentDto dto)
        {
            // return fake id & maybe history entries
            return Task.FromResult(new SaveResult
            {
                Success = true,
                Id = dto.Id ?? 123,
                History = new List<HistoryDto>
                {
                    new HistoryDto{ ChangedAtUtc=DateTime.UtcNow, ChangedBy="user", EntityType="Allotment", Property="Title", OldValue=dto.Title, NewValue=dto.Title }
                }
            });
        }
    }

    public interface IWindowNavigator { void CloseWindow(); }
    public class DummyWindowNavigator : IWindowNavigator
    {
        public void CloseWindow()
        {
            // συνήθως στέλνεις event/Mediator ή AttachedBehavior για Close
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        private readonly Predicate<object?>? _can;
        public RelayCommand(Action<object?> exec, Predicate<object?>? can = null) { _exec = exec; _can = can; }
        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
