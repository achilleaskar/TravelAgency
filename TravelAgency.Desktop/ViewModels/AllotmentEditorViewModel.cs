// Desktop/ViewModels/AllotmentEditorViewModel.cs
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore; // kept for future use; not required by this VM now
using TravelAgency.Data;            // kept so the existing window ctor signature compiles
using TravelAgency.Desktop.Helpers;
using TravelAgency.Domain.Dtos;     // AllotmentDto, AllotmentLineDto, PaymentDto, HistoryDto
using TravelAgency.Services;        // IAllotmentService, CityVM, HotelVM, RoomTypeVM

namespace TravelAgency.Desktop.ViewModels
{
    public class AllotmentEditorViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly IAllotmentService _svc;

        // retained only so the current window code-behind compiles; not used by this VM
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        private readonly Dictionary<string, List<string>> _errors = new();

        public event Action<bool>? CloseRequested;

        private bool _isNew;
        private int? _allotmentId;
        private bool _isDirty;




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

            Lines.CollectionChanged += (_, __) =>
            {
                RecalcTotals(); MarkDirty(); RaiseCanExec();
            };
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

        // IMPORTANT: repo uses AllotmentDatePolicy (not DatePolicy)
        private string _allotmentDatePolicy = "ExactDates"; // "ExactDates" | "PartialAllowed"
        public string AllotmentDatePolicy
        {
            get => _allotmentDatePolicy;
            set { if (Set(ref _allotmentDatePolicy, value)) MarkDirty(); }
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
            using (Busy.Begin())
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
                AllotmentDatePolicy = "ExactDates";

                Lines.Clear();
                Payments.Clear();
                History.Clear();

                _isDirty = false;
                RaiseAll();
            }
        }


        public async Task InitializeForEditAsync(int id)
        {
            using (Busy.Begin())
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
                AllotmentDatePolicy = dto.AllotmentDatePolicy;

                Lines.Clear();
                foreach (var l in dto.Lines)
                {
                    var vm = new AllotmentLineVM
                    {
                        Id = l.Id,
                        RoomType = RoomTypes.FirstOrDefault(r => r.Id == l.RoomTypeId),
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Notes = l.Notes
                    };
                    HookLine(vm);
                    Lines.Add(vm);
                }

                Payments.Clear();
                foreach (var p in dto.Payments.OrderBy(p => p.DateUtc))
                {
                    var vm = new PaymentVM
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
                    HookPayment(vm);
                    Payments.Add(vm);
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

                _isDirty = false;
                RecalcTotals();
                RaiseAll();
            }
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
                Notes = ""
            };
            HookLine(vm);
            Lines.Add(vm);
        }

        private void HookLine(AllotmentLineVM vm)
        {
            vm.ResolveRoomTypeFromId = (id) =>
            {
                var rt = RoomTypes.FirstOrDefault(r => r.Id == id);
                if (!ReferenceEquals(vm.RoomType, rt)) vm.RoomType = rt;
            };

            vm.PropertyChanged += (_, __) =>
            {
                RecalcTotals();
                MarkDirty();
                RaiseCanExec();
            };
        }

        private void HookPayment(PaymentVM vm)
        {
            vm.PropertyChanged += (_, __) =>
            {
                RecalcTotals();      // Amount / IsVoided changes update totals
                MarkDirty();         // enable Save (or at least mark CanSave)
                RaiseCanExec();      // re-evaluate commands
                OnPropertyChanged(nameof(CanSave)); // if you're binding IsEnabled to CanSave
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
            var vm = new PaymentVM
            {
                Date = DateTime.Today,
                Title = "Payment",
                Kind = "Deposit",
                Amount = 0m,
                Notes = "",
                IsVoided = false
            };
            HookPayment(vm);          // <-- add this
            Payments.Add(vm);
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

            using (Busy.Begin())
            {
                var dto = new AllotmentDto
                {
                    Id = _allotmentId,
                    Title = Title,
                    CityId = SelectedCity!.Id,
                    HotelId = SelectedHotel!.Id,
                    StartDateUtc = (StartDate ?? DateTime.Today).ToUniversalTime(),
                    EndDateUtc = (EndDate ?? DateTime.Today).ToUniversalTime(),
                    OptionDueUtc = OptionDueDate?.ToUniversalTime(),
                    AllotmentDatePolicy = AllotmentDatePolicy,
                    Lines = Lines.Select(l => new AllotmentLineDto
                    {
                        Id = l.Id,
                        RoomTypeId = l.RoomType?.Id ?? 0,
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
                        IsVoided = p.IsVoided
                    }).ToList()
                };

                var result = await _svc.SaveAsync(dto);
                if (!result.Success) return;

                _allotmentId = result.Id;
                _isNew = false;

                if (result.History?.Count > 0)
                {
                    History.Clear();
                    foreach (var h in result.History.OrderByDescending(x => x.ChangedAtUtc))
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
                }

                RecalcTotals();
                _isDirty = false;
                OnPropertyChanged(nameof(HeaderTitle));
                RaiseCanExec();

                CloseRequested?.Invoke(true);
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

            if (SelectedHotel != null && !FilteredHotels.Contains(SelectedHotel))
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

    #region Child VMs

    #endregion
}
