using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class AllotmentsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        public AllotmentsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

        public ObservableCollection<Allotment> Allotments { get; } = new();
        public ObservableCollection<Hotel> Hotels { get; } = new();
        public ObservableCollection<AllotmentRoomType> Lines { get; } = new();
        public ObservableCollection<RoomType> AllRoomTypes { get; } = new();
        public ObservableCollection<AllotmentStatus> Statuses { get; } = new(Enum.GetValues<AllotmentStatus>());

        [ObservableProperty] private Allotment? selected;
        [ObservableProperty] private AllotmentRoomType? selectedLine;

        // Filters
        [ObservableProperty] private string? searchText;
        [ObservableProperty] private Hotel? filterHotel;
        [ObservableProperty] private AllotmentStatus? filterStatus;

        // Editor state
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the grid on the left to select an allotment.";

        [ObservableProperty] private string? editTitle;
        [ObservableProperty] private Hotel? editHotel;
        [ObservableProperty] private DateTime? editStartDate = DateTime.Today;
        [ObservableProperty] private DateTime? editEndDate = DateTime.Today.AddDays(3);
        [ObservableProperty] private DateTime? editOptionDue;
        [ObservableProperty] private AllotmentStatus? editStatus = AllotmentStatus.Active;
        [ObservableProperty] private string? editNotes;

        // Line editor
        [ObservableProperty] private RoomType? lineRoomType;
        [ObservableProperty] private string? lineQty = "0";
        [ObservableProperty] private string? linePrice = "0";
        [ObservableProperty] private string? lineCurrency = "EUR";
        [ObservableProperty] private bool lineSpecific;
        [ObservableProperty] private bool lineCancelled;

        private bool _isNewMode;
        private int? _editingId;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(Allotment? value)
        {
              using var db =   _dbf.CreateDbContext();

            Lines.Clear();
            if (value != null)
            {
                foreach (var l in db.AllotmentRoomTypes.Include(x => x.RoomType)
                         .Where(x => x.AllotmentId == value.Id).AsNoTracking())
                    Lines.Add(l);
            }
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Hotels.Clear();
            foreach (var h in await db.Hotels.OrderBy(x => x.Name).ToListAsync()) Hotels.Add(h);

            AllRoomTypes.Clear();
            foreach (var rt in await db.RoomTypes.OrderBy(x => x.Name).ToListAsync()) AllRoomTypes.Add(rt);

            var q = db.Allotments.Include(a => a.Hotel).AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(a => a.Title.Contains(SearchText) || a.Hotel!.Name.Contains(SearchText));

            if (FilterHotel != null)
                q = q.Where(a => a.HotelId == FilterHotel.Id);

            if (FilterStatus != null)
                q = q.Where(a => a.Status == FilterStatus);

            Allotments.Clear();
            foreach (var a in await q.AsNoTracking().OrderByDescending(a => a.StartDate).ToListAsync())
                Allotments.Add(a);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;
            EditTitle = ""; EditNotes = "";
            EditHotel = Hotels.FirstOrDefault();
            EditStartDate = DateTime.Today; EditEndDate = DateTime.Today.AddDays(3);
            EditOptionDue = null; EditStatus = AllotmentStatus.Active;
            EditorTitle = "Add New Allotment";
            EditorHint = "Fill the fields and add room-type lines below.";
            Lines.Clear();
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;
            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;
            EditTitle = Selected.Title;
            EditHotel = Hotels.FirstOrDefault(h => h.Id == Selected.HotelId);
            EditStartDate = Selected.StartDate;
            EditEndDate = Selected.EndDate;
            EditOptionDue = Selected.OptionDueDate;
            EditStatus = Selected.Status;
            EditNotes = Selected.Notes;
            EditorTitle = $"Edit Allotment #{Selected.Id}";
            EditorHint = "Modify fields and lines; click Save when ready.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (EditHotel == null || string.IsNullOrWhiteSpace(EditTitle) ||
                EditStartDate == null || EditEndDate == null) return;

            if (_isNewMode)
            {
                var a = new Allotment
                {
                    Title = EditTitle!.Trim(),
                    HotelId = EditHotel.Id,
                    StartDate = EditStartDate!.Value.Date,
                    EndDate = EditEndDate!.Value.Date,
                    OptionDueDate = EditOptionDue,
                    Status = EditStatus ?? AllotmentStatus.Active,
                    Notes = EditNotes
                };
                db.Allotments.Add(a);
                await db.SaveChangesAsync();

                // persist added lines
                foreach (var l in Lines)
                {
                    l.AllotmentId = a.Id;
                    db.AllotmentRoomTypes.Add(new AllotmentRoomType
                    {
                        AllotmentId = a.Id,
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = l.Currency,
                        IsSpecific = l.IsSpecific,
                        IsCancelled = l.IsCancelled
                    });
                }
            }
            else if (_editingId.HasValue)
            {
                var a = await db.Allotments.FirstAsync(x => x.Id == _editingId.Value);
                a.Title = EditTitle!.Trim();
                a.HotelId = EditHotel.Id;
                a.StartDate = EditStartDate!.Value.Date;
                a.EndDate = EditEndDate!.Value.Date;
                a.OptionDueDate = EditOptionDue;
                a.Status = EditStatus ?? AllotmentStatus.Active;
                a.Notes = EditNotes;

                // crude sync of lines: delete + re-add (OK for MVP)
                var old = db.AllotmentRoomTypes.Where(x => x.AllotmentId == a.Id);
                db.AllotmentRoomTypes.RemoveRange(old);
                foreach (var l in Lines)
                {
                    db.AllotmentRoomTypes.Add(new AllotmentRoomType
                    {
                        AllotmentId = a.Id,
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = l.Currency,
                        IsSpecific = l.IsSpecific,
                        IsCancelled = l.IsCancelled
                    });
                }
            }

            await db.SaveChangesAsync();
            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;
            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the grid on the left to select an allotment.";
            if (Selected != null) OnSelectedChanged(Selected);
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (Selected == null) return;
            var id = Selected.Id;
            var lines = db.AllotmentRoomTypes.Where(x => x.AllotmentId == id);
            db.AllotmentRoomTypes.RemoveRange(lines);
            db.Allotments.Remove(await db.Allotments.FirstAsync(x => x.Id == id));
            await db.SaveChangesAsync();
            await LoadAsync();
        }

        // Lines
        [RelayCommand]
        private void UpsertLine()
        {
            if (LineRoomType == null) return;
            if (!int.TryParse(LineQty ?? "0", out var qty)) qty = 0;
            if (!decimal.TryParse(LinePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price)) price = 0m;

            if (SelectedLine == null)
            {
                Lines.Add(new AllotmentRoomType
                {
                    RoomTypeId = LineRoomType.Id,
                    RoomType = LineRoomType,
                    Quantity = qty,
                    PricePerNight = price,
                    Currency = string.IsNullOrWhiteSpace(LineCurrency) ? "EUR" : LineCurrency!,
                    IsSpecific = LineSpecific,
                    IsCancelled = LineCancelled
                });
            }
            else
            {
                SelectedLine.RoomTypeId = LineRoomType.Id;
                SelectedLine.RoomType = LineRoomType;
                SelectedLine.Quantity = qty;
                SelectedLine.PricePerNight = price;
                SelectedLine.Currency = string.IsNullOrWhiteSpace(LineCurrency) ? "EUR" : LineCurrency!;
                SelectedLine.IsSpecific = LineSpecific;
                SelectedLine.IsCancelled = LineCancelled;
                OnPropertyChanged(nameof(Lines));
            }

            // reset line editor
            LineRoomType = null; LineQty = "0"; LinePrice = "0"; LineCurrency = "EUR";
            LineSpecific = false; LineCancelled = false;
        }

        [RelayCommand]
        private void RemoveLine()
        {
            if (SelectedLine == null) return;
            Lines.Remove(SelectedLine);
        }
    }
}
