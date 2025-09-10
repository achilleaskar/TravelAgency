using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

public class ReservationEditorViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly IReservationService _svc;
    private readonly LookupCacheService _cache;

    public ReservationEditorViewModel(IReservationService svc, LookupCacheService cache)
    {
        _svc = svc; _cache = cache;

        AddLineCommand = new RelayCommand(_ => AddLine());
        RemoveSelectedLineCommand = new RelayCommand(_ => RemoveSelectedLine(), _ => SelectedLine != null);
        AddPaymentCommand = new RelayCommand(_ => AddPayment());
        RemoveSelectedPaymentCommand = new RelayCommand(_ => RemoveSelectedPayment(), _ => SelectedPayment != null);
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));

        Customers = new ObservableCollection<CustomerVM>();
        Cities = new ObservableCollection<CityVM>();
        Hotels = new ObservableCollection<HotelVM>();
        FilteredHotels = new ObservableCollection<HotelVM>();
        RoomTypes = new ObservableCollection<RoomTypeVM>();
        Lines = new ObservableCollection<ReservationLineVM>();
        Payments = new ObservableCollection<PaymentVM>();
        History = new ObservableCollection<UpdateLogVM>();

        Lines.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); OnPropertyChanged(nameof(CanSave)); };
        Payments.CollectionChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
    }

    public event Action<bool>? CloseRequested;

    public ObservableCollection<CustomerVM> Customers { get; }
    public ObservableCollection<CityVM> Cities { get; }
    public ObservableCollection<HotelVM> Hotels { get; }
    public ObservableCollection<HotelVM> FilteredHotels { get; }
    public ObservableCollection<RoomTypeVM> RoomTypes { get; }
    public ObservableCollection<ReservationLineVM> Lines { get; }
    public ObservableCollection<PaymentVM> Payments { get; }
    public ObservableCollection<UpdateLogVM> History { get; }

    public string HeaderTitle => _isNew ? "New Reservation" : $"Edit Reservation #{_id}";
    private bool _isNew; private int? _id; private bool _dirty;

    public int CustomerId { get => _customerId; set { if (Set(ref _customerId, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }
    private int _customerId;

    public CityVM? SelectedCity { get => _city; set { if (Set(ref _city, value)) { ApplyHotelFilter(); MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }
    private CityVM? _city;

    public HotelVM? SelectedHotel { get => _hotel; set { if (Set(ref _hotel, value)) { MarkDirty(); OnPropertyChanged(nameof(CanSave)); } } }
    private HotelVM? _hotel;

    public DateTime? CheckIn { get => _in; set { if (Set(ref _in, value)) { ValidateDates(); RecalcTotals(); MarkDirty(); } } }
    private DateTime? _in = DateTime.Today;

    public DateTime? CheckOut { get => _out; set { if (Set(ref _out, value)) { ValidateDates(); RecalcTotals(); MarkDirty(); } } }
    private DateTime? _out = DateTime.Today.AddDays(1);

    public int Nights { get => _nights; private set => Set(ref _nights, value); }
    private int _nights;
    public decimal Total { get => _total; private set => Set(ref _total, value); }
    private decimal _total;
    public decimal PaidTotal { get => _paid; private set => Set(ref _paid, value); }
    private decimal _paid;
    public decimal Balance { get => _balance; private set => Set(ref _balance, value); }
    private decimal _balance;

    public ReservationLineVM? SelectedLine { get => _selLine; set { if (Set(ref _selLine, value)) RaiseCanExec(); } }
    private ReservationLineVM? _selLine;
    public PaymentVM? SelectedPayment { get => _selPay; set { if (Set(ref _selPay, value)) RaiseCanExec(); } }
    private PaymentVM? _selPay;

    public bool CanSave => _dirty && !HasErrors && SelectedHotel != null && SelectedCity != null && Lines.Any() && CustomerId > 0;

    public ICommand AddLineCommand { get; }
    public ICommand RemoveSelectedLineCommand { get; }
    public ICommand AddPaymentCommand { get; }
    public ICommand RemoveSelectedPaymentCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task InitializeAsNewAsync()
    {
        _isNew = true; _id = null;
        await LoadLookupsAsync();
        SelectedCity = Cities.FirstOrDefault();
        ApplyHotelFilter();
        SelectedHotel = null;
        Lines.Clear(); Payments.Clear(); History.Clear();
        _dirty = false; RaiseAll();
    }

    public async Task InitializeForEditAsync(int id)
    {
        _isNew = false; _id = id;
        await LoadLookupsAsync();

        var dto = await _svc.LoadAsync(id);
        CustomerId = dto.CustomerId;
        SelectedCity = Cities.FirstOrDefault(c => c.Id == Hotels.First(h => h.Id == dto.HotelId).CityId);
        ApplyHotelFilter();
        SelectedHotel = FilteredHotels.FirstOrDefault(h => h.Id == dto.HotelId);
        CheckIn = dto.CheckInUtc.ToLocalTime().Date;
        CheckOut = dto.CheckOutUtc.ToLocalTime().Date;

        Lines.Clear();
        foreach (var l in dto.Lines)
        {
            var vm = new ReservationLineVM
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

        _dirty = false; RecalcTotals(); RaiseAll();
    }

    private async Task LoadLookupsAsync()
    {
        Customers.Clear(); Cities.Clear(); Hotels.Clear(); RoomTypes.Clear();
        var (customers, cities, hotels, roomTypes) = await _svc.LoadLookupsAsync();
        foreach (var x in customers) Customers.Add(x);
        foreach (var x in cities) Cities.Add(x);
        foreach (var x in hotels) Hotels.Add(x);
        foreach (var x in roomTypes) RoomTypes.Add(x);
    }

    private void AddLine()
    {
        var vm = new ReservationLineVM { RoomType = RoomTypes.FirstOrDefault(), Quantity = 1, PricePerNight = 0m };
        HookLine(vm); Lines.Add(vm);
    }
    private void HookLine(ReservationLineVM vm)
    {
        vm.ResolveRoomTypeFromId = id =>
        {
            var rt = RoomTypes.FirstOrDefault(r => r.Id == id);
            if (!ReferenceEquals(vm.RoomType, rt)) vm.RoomType = rt;
        };
        vm.PropertyChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
    }
    private void RemoveSelectedLine() { if (SelectedLine == null) return; Lines.Remove(SelectedLine); RecalcTotals(); MarkDirty(); RaiseCanExec(); }

    private void AddPayment()
    {
        var vm = new PaymentVM { Date = DateTime.Today, Title = "Payment", Kind = "Deposit", Amount = 0m, IsVoided = false };
        HookPayment(vm); Payments.Add(vm);
    }
    private void HookPayment(PaymentVM vm)
    {
        vm.PropertyChanged += (_, __) => { RecalcTotals(); MarkDirty(); RaiseCanExec(); };
    }
    private void RemoveSelectedPayment() { if (SelectedPayment == null) return; Payments.Remove(SelectedPayment); RecalcTotals(); MarkDirty(); RaiseCanExec(); }

    private void RecalcTotals()
    {
        var n = 0;
        if (CheckIn.HasValue && CheckOut.HasValue) n = Math.Max(0, (CheckOut.Value.Date - CheckIn.Value.Date).Days);
        Nights = n;

        decimal total = 0m;
        foreach (var l in Lines) total += l.PricePerNight * l.Quantity * n;
        Total = total;

        decimal paid = 0m;
        foreach (var p in Payments) if (!p.IsVoided) paid += p.Amount;
        PaidTotal = paid;

        Balance = Total - PaidTotal;

        foreach (var l in Lines) l.SetNightsForLineTotal(n);
    }

    private void ApplyHotelFilter()
    {
        FilteredHotels.Clear();
        if (SelectedCity == null) return;
        foreach (var h in Hotels.Where(h => h.CityId == SelectedCity.Id)) FilteredHotels.Add(h);
        if (SelectedHotel != null && !FilteredHotels.Contains(SelectedHotel)) SelectedHotel = null;
    }

    private void MarkDirty() { _dirty = true; RaiseCanExec(); OnPropertyChanged(nameof(CanSave)); }
    private void RaiseCanExec()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSelectedLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSelectedPaymentCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
    private void RaiseAll() { OnPropertyChanged(string.Empty); RaiseCanExec(); }

    // validation like in Allotments, omitted for brevity (require customer, hotel, dates, lines)

    // Save
    private async Task SaveAsync()
    {
        // (do validation like your Allotment VM)
        if (!CanSave) return;

        var dto = new ReservationDto
        {
            Id = _id,
            CustomerId = CustomerId,
            HotelId = SelectedHotel!.Id,
            CheckInUtc = (CheckIn ?? DateTime.Today).ToUniversalTime(),
            CheckOutUtc = (CheckOut ?? DateTime.Today).ToUniversalTime(),
            Lines = Lines.Select(l => new ReservationLineDto
            {
                Id = l.Id,
                RoomTypeId = l.RoomTypeId,
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

        _id = result.Id; _isNew = false; _dirty = false;
        OnPropertyChanged(nameof(HeaderTitle));
        RaiseCanExec();
        CloseRequested?.Invoke(true);
    }

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
