// Desktop/ViewModels/AllotmentEditorViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.ViewModels;

public partial class AllotmentEditorViewModel : ObservableObject
{
    private readonly IDbContextFactory<YourDbContext> _dbf;
    private readonly int? _allotmentId;

    public ObservableCollection<Hotel> Hotels { get; } = new();
    public ObservableCollection<RoomType> RoomTypes { get; } = new();

    // master
    [ObservableProperty] private Hotel? selectedHotel;
    [ObservableProperty] private string? title;
    [ObservableProperty] private DateTime? startDate = DateTime.Today;
    [ObservableProperty] private DateTime? endDate = DateTime.Today.AddDays(3);
    [ObservableProperty] private DateTime? optionDueDate;
    [ObservableProperty] private AllotmentStatus status = AllotmentStatus.Active;
    [ObservableProperty] private AllotmentDatePolicy datePolicy = AllotmentDatePolicy.ExactDates;
    [ObservableProperty] private string? notes;

    // lines
    public ObservableCollection<LineVM> Lines { get; } = new();
    [ObservableProperty] private LineVM? selectedLine;

    // payments
    public ObservableCollection<PaymentVM> Payments { get; } = new();
    [ObservableProperty] private PaymentVM? selectedPayment;

    // derived totals
    public int Nights => StartDate.HasValue && EndDate.HasValue
        ? Math.Max(0, (EndDate.Value.Date - StartDate.Value.Date).Days)
        : 0;

    public decimal BaseCost => Lines.Sum(l => l.Quantity * l.PricePerNight * Nights);
    public decimal PaidTotal => Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
    public decimal Balance => BaseCost - PaidTotal;

    public AllotmentEditorViewModel(IDbContextFactory<YourDbContext> dbf, int? allotmentId = null)
    {
        _dbf = dbf;
        _allotmentId = allotmentId;
    }

    public async Task InitializeAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();

        Hotels.Clear();
        foreach (var h in await db.Hotels.AsNoTracking().OrderBy(x => x.Name).ToListAsync()) Hotels.Add(h);

        RoomTypes.Clear();
        foreach (var rt in await db.RoomTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync()) RoomTypes.Add(rt);

        if (_allotmentId.HasValue)
        {
            var a = await db.Allotments
                .Include(x => x.RoomTypes).ThenInclude(l => l.RoomType)
                .Include(x => x.Payments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == _allotmentId.Value);

            SelectedHotel = Hotels.FirstOrDefault(x => x.Id == a.HotelId);
            Title = a.Title;
            StartDate = a.StartDate;
            EndDate = a.EndDate;
            OptionDueDate = a.OptionDueDate;
            Status = a.Status;
            DatePolicy = a.DatePolicy;
            Notes = a.Notes;

            Lines.Clear();
            foreach (var l in a.RoomTypes.OrderBy(x => x.RoomType!.Name))
                Lines.Add(new LineVM
                {
                    Id = l.Id,
                    RoomType = RoomTypes.FirstOrDefault(rt => rt.Id == l.RoomTypeId),
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Currency = l.Currency,
                    Notes = l.Notes
                });

            Payments.Clear();
            foreach (var p in a.Payments.OrderBy(x => x.Date))
                Payments.Add(new PaymentVM
                {
                    Id = p.Id,
                    Date = p.Date,
                    Title = p.Title,
                    Kind = p.Kind,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided
                });
        }

        OnPropertyChanged(nameof(Nights));
        OnPropertyChanged(nameof(BaseCost));
        OnPropertyChanged(nameof(PaidTotal));
        OnPropertyChanged(nameof(Balance));
    }

    // --- Lines commands ---
    [RelayCommand]
    private void NewLine()
    {
        Lines.Add(new LineVM
        {
            RoomType = RoomTypes.FirstOrDefault(),
            Quantity = 0,
            PricePerNight = 0m,
            Currency = "EUR"
        });
    }

    [RelayCommand]
    private void RemoveLine() { if (SelectedLine != null) Lines.Remove(SelectedLine); }

    // --- Payments commands ---
    [RelayCommand]
    private void NewPayment()
    {
        Payments.Add(new PaymentVM
        {
            Date = DateTime.Today,
            Title = "Payment",
            Kind = PaymentKind.Other,
            Amount = 0m,
            Currency = "EUR",
            IsVoided = false
        });
        OnPropertyChanged(nameof(PaidTotal)); OnPropertyChanged(nameof(Balance));
    }

    [RelayCommand]
    private void RemovePayment()
    {
        if (SelectedPayment == null) return;
        Payments.Remove(SelectedPayment);
        OnPropertyChanged(nameof(PaidTotal)); OnPropertyChanged(nameof(Balance));
    }

    // --- Save / Cancel ---
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedHotel == null || !StartDate.HasValue || !EndDate.HasValue) return;

        await using var db = await _dbf.CreateDbContextAsync();

        Allotment entity;
        if (_allotmentId.HasValue)
        {
            entity = await db.Allotments
                .Include(x => x.RoomTypes)
                .Include(x => x.Payments)
                .FirstAsync(x => x.Id == _allotmentId.Value);

            entity.HotelId = SelectedHotel.Id;
            entity.Title = Title?.Trim();
            entity.StartDate = StartDate.Value.Date;
            entity.EndDate = EndDate.Value.Date;
            entity.OptionDueDate = OptionDueDate;
            entity.Status = Status;
            entity.DatePolicy = DatePolicy;
            entity.Notes = Notes;

            // Upsert Lines (χωρίς delete+readd → κρατάς audit per property)
            // 1) υπαρχοντα by id
            var byId = entity.RoomTypes.ToDictionary(x => x.Id, x => x);
            foreach (var lvm in Lines)
            {
                if (lvm.Id == 0)
                {
                    entity.RoomTypes.Add(new AllotmentRoomType
                    {
                        RoomTypeId = lvm.RoomType!.Id,
                        Quantity = lvm.Quantity,
                        PricePerNight = lvm.PricePerNight,
                        Currency = lvm.Currency ?? "EUR",
                        Notes = lvm.Notes
                    });
                }
                else if (byId.TryGetValue(lvm.Id, out var ex))
                {
                    ex.RoomTypeId = lvm.RoomType!.Id;
                    ex.Quantity = lvm.Quantity;
                    ex.PricePerNight = lvm.PricePerNight;
                    ex.Currency = lvm.Currency ?? "EUR";
                    ex.Notes = lvm.Notes;
                }
            }
            // 2) deletions
            var keepIds = Lines.Where(x => x.Id != 0).Select(x => x.Id).ToHashSet();
            var toRemove = entity.RoomTypes.Where(x => !keepIds.Contains(x.Id)).ToList();
            foreach (var del in toRemove) db.AllotmentRoomTypes.Remove(del);

            // Payments upsert
            var payById = entity.Payments.ToDictionary(x => x.Id, x => x);
            foreach (var pvm in Payments)
            {
                if (pvm.Id == 0)
                {
                    entity.Payments.Add(new AllotmentPayment
                    {
                        Date = pvm.Date,
                        Title = pvm.Title ?? "Payment",
                        Kind = pvm.Kind,
                        Amount = pvm.Amount,
                        Currency = pvm.Currency ?? "EUR",
                        Notes = pvm.Notes,
                        IsVoided = pvm.IsVoided
                    });
                }
                else if (payById.TryGetValue(pvm.Id, out var ex))
                {
                    ex.Date = pvm.Date;
                    ex.Title = pvm.Title ?? "Payment";
                    ex.Kind = pvm.Kind;
                    ex.Amount = pvm.Amount;
                    ex.Currency = pvm.Currency ?? "EUR";
                    ex.Notes = pvm.Notes;
                    ex.IsVoided = pvm.IsVoided;
                }
            }
            var keepPayIds = Payments.Where(x => x.Id != 0).Select(x => x.Id).ToHashSet();
            var payRemove = entity.Payments.Where(x => !keepPayIds.Contains(x.Id)).ToList();
            foreach (var del in payRemove) db.AllotmentPayments.Remove(del);
        }
        else
        {
            entity = new Allotment
            {
                HotelId = SelectedHotel.Id,
                Title = Title?.Trim(),
                StartDate = StartDate.Value.Date,
                EndDate = EndDate.Value.Date,
                OptionDueDate = OptionDueDate,
                Status = Status,
                DatePolicy = DatePolicy,
                Notes = Notes
            };

            foreach (var l in Lines)
            {
                entity.RoomTypes.Add(new AllotmentRoomType
                {
                    RoomTypeId = l.RoomType!.Id,
                    Quantity = l.Quantity,
                    PricePerNight = l.PricePerNight,
                    Currency = l.Currency ?? "EUR",
                    Notes = l.Notes
                });
            }
            foreach (var p in Payments)
            {
                entity.Payments.Add(new AllotmentPayment
                {
                    Date = p.Date,
                    Title = p.Title ?? "Payment",
                    Kind = p.Kind,
                    Amount = p.Amount,
                    Currency = p.Currency ?? "EUR",
                    Notes = p.Notes,
                    IsVoided = p.IsVoided
                });
            }

            db.Allotments.Add(entity);
        }

        await db.SaveChangesAsync();
        CloseWindow();
    }

    [RelayCommand] private void Cancel() => CloseWindow();

    private void CloseWindow()
    {
        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            if (w.DataContext == this) { w.Close(); break; }
    }

    // --- inner VMs ---
    public partial class LineVM : ObservableObject
    {
        public int Id { get; set; }
        [ObservableProperty] private RoomType? roomType;
        [ObservableProperty] private int quantity;
        [ObservableProperty] private decimal pricePerNight;
        [ObservableProperty] private string? currency = "EUR";
        [ObservableProperty] private string? notes;
        public decimal LineTotal(int nights) => Quantity * PricePerNight * nights;
    }

    public partial class PaymentVM : ObservableObject
    {
        public int Id { get; set; }
        [ObservableProperty] private DateTime date = DateTime.Today;
        [ObservableProperty] private string? title = "Payment";
        [ObservableProperty] private PaymentKind kind = PaymentKind.Other;
        [ObservableProperty] private decimal amount;
        [ObservableProperty] private string? currency = "EUR";
        [ObservableProperty] private string? notes;
        [ObservableProperty] private bool isVoided;
    }
}
