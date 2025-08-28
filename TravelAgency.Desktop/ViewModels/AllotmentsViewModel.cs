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
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class AllotmentsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public ObservableCollection<Allotment> Allotments { get; } = new();
        public ObservableCollection<AllotmentRoomType> Lines { get; } = new();

        // Filters (dropdowns from cache)
        public ObservableCollection<Hotel> Hotels => _cache.Hotels;
        public ObservableCollection<RoomType> AllRoomTypes => _cache.RoomTypes;
        public ObservableCollection<AllotmentStatus> Statuses { get; } = new(Enum.GetValues<AllotmentStatus>());

        [ObservableProperty] private Allotment? selected;
        [ObservableProperty] private AllotmentRoomType? selectedLine;

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

        public AllotmentsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;
        }
        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(Allotment? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
            RefreshLinesForSelected();

            // If user started "Add New" then clicked a row, flip to edit mode
            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditTitle = value.Title;
                EditHotel = Hotels.FirstOrDefault(h => h.Id == value.HotelId);
                EditStartDate = value.StartDate;
                EditEndDate = value.EndDate;
                EditOptionDue = value.OptionDueDate;
                EditStatus = value.Status;
                EditNotes = value.Notes;

                EditorTitle = $"Edit Allotment #{value.Id}";
                EditorHint = "Modify fields and lines; click Save.";
            }
        }

        private void RefreshLinesForSelected()
        {
            Lines.Clear();
            if (Selected == null) return;

            using var db = _dbf.CreateDbContext();
            var rows = db.AllotmentRoomTypes
                         .Include(x => x.RoomType)
                         .Where(x => x.AllotmentId == Selected.Id)
                         .AsNoTracking()
                         .ToList();
            foreach (var l in rows) Lines.Add(l);
        }

        private void ResetLineEditor()
        {
            LineRoomType = null;
            LineQty = "0";
            LinePrice = "0";
            LineCurrency = "EUR";
            LineSpecific = false;
            LineCancelled = false;
            SelectedLine = null;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var q = db.Allotments.Include(a => a.Hotel).AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(a => a.Title.Contains(SearchText) || a.Hotel!.Name.Contains(SearchText));

            if (FilterHotel != null)
                q = q.Where(a => a.HotelId == FilterHotel.Id);

            if (FilterStatus != null)
                q = q.Where(a => a.Status == FilterStatus);

            var list = await q.AsNoTracking()
                              .OrderByDescending(a => a.StartDate)
                              .ToListAsync();

            Allotments.Clear();
            foreach (var a in list) Allotments.Add(a);

            // keep current lines in sync if a row is selected
            if (Selected != null) RefreshLinesForSelected();
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;

            Selected = null; // allow a row click to switch to edit
            EditTitle = ""; EditNotes = "";
            EditHotel = Hotels.FirstOrDefault();
            EditStartDate = DateTime.Today;
            EditEndDate = DateTime.Today.AddDays(3);
            EditOptionDue = null;
            EditStatus = AllotmentStatus.Active;

            Lines.Clear();
            ResetLineEditor();

            EditorTitle = "Add New Allotment";
            EditorHint = "Fill the fields and add room-type lines below.";
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

            RefreshLinesForSelected();
            ResetLineEditor();

            EditorTitle = $"Edit Allotment #{Selected.Id}";
            EditorHint = "Modify fields and lines; click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (EditHotel == null || string.IsNullOrWhiteSpace(EditTitle) ||
                EditStartDate == null || EditEndDate == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

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

                // persist lines
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
                await db.SaveChangesAsync();
            }
            else if (_editingId.HasValue)
            {
                var a = await db.Allotments.FirstAsync(x => x.Id == _editingId.Value);
                a.Title = EditTitle!.Trim();
                a.HotelId = EditHotel.Id;
                a.StartDate = EditStartDate!.Value.Date;
                a.EndDate = EditEndDate!.Value.Date;
                a.OptionDueDate = EditOptionDue;
                a.Status = EditStatus ?? a.Status;
                a.Notes = EditNotes;

                // sync lines: delete & re-add (simple + safe for MVP)
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
                await db.SaveChangesAsync();
            }

            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false;
            _isNewMode = false;
            _editingId = null;

            RefreshLinesForSelected();
            ResetLineEditor();

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the grid on the left to select an allotment.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

            var lines = db.AllotmentRoomTypes.Where(x => x.AllotmentId == Selected.Id);
            db.AllotmentRoomTypes.RemoveRange(lines);
            var entity = await db.Allotments.FirstAsync(x => x.Id == Selected.Id);
            db.Allotments.Remove(entity);
            await db.SaveChangesAsync();

            await LoadAsync();
        }

        // Lines commands
        [RelayCommand]
        private void UpsertLine()
        {
            if (LineRoomType == null) return;
            if (!int.TryParse(LineQty ?? "0", out var qty)) qty = 0;
            if (!decimal.TryParse(LinePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                price = 0m;

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

            ResetLineEditor();
        }

        [RelayCommand]
        private void RemoveLine()
        {
            if (SelectedLine == null) return;
            Lines.Remove(SelectedLine);
            ResetLineEditor();
        }
    }
}
