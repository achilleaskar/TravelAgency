using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Desktop.Helpers;
using TravelAgency.Desktop.Views;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class AllotmentsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public ObservableCollection<AllotmentRow> Allotments { get; } = new();

        // Lines displayed in UI (projection with Sold/Remaining)
        public ObservableCollection<LineRow> Lines { get; } = new();

        // Filters
        public ObservableCollection<Hotel> Hotels => _cache.Hotels;

        public ObservableCollection<RoomType> AllRoomTypes => _cache.RoomTypes;
        public ObservableCollection<AllotmentStatus> Statuses { get; } = new(Enum.GetValues<AllotmentStatus>());

        [ObservableProperty] private AllotmentRow? selected;
        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;
        [ObservableProperty] private LineRow? selectedLine;

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

        // Line editor (create/update)
        [ObservableProperty] private RoomType? lineRoomType;

        [ObservableProperty] private string? lineTotalQty = "0";
        [ObservableProperty] private string? linePrice = "0";

        [RelayCommand]
        private async Task OpenNewDialogAsync()
        {
            using (Busy.Begin())
            {
                var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                var win = new AllotmentEditorWindow(allotmentId: null) { Owner = owner };
                var ok = win.ShowDialog() ?? false;
                if (ok) await LoadAsync();
            }
        }

        [RelayCommand]
        private async Task OpenEditDialogAsync()
        {
            if (Selected == null) return;

            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            var win = new AllotmentEditorWindow(Selected.Id) { Owner = owner };
            var ok = win.ShowDialog() ?? false;
            if (ok) await LoadAsync();
        }

        // Cancel units input
        [ObservableProperty] private string? lineCancelQty;

        private bool _isNewMode;
        private int? _editingId;

        public AllotmentsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf; _cache = cache;
        }

        partial void OnSelectedChanged(AllotmentRow? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
            RefreshLinesForSelected();

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
                EditNotes = value.Notes;   // see step 3

                EditorTitle = $"Edit Allotment #{value.Id}";
                EditorHint = "Modify fields and lines; click Save.";
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var qA = db.Allotments.Include(a => a.Hotel).AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                qA = qA.Where(a => a.Title.Contains(SearchText) || a.Hotel!.Name.Contains(SearchText));

            if (FilterHotel != null)
                qA = qA.Where(a => a.HotelId == FilterHotel.Id);

            if (FilterStatus != null)
                qA = qA.Where(a => a.Status == FilterStatus);

            var allotments = await qA.AsNoTracking()
                                     .OrderByDescending(a => a.StartDate)
                                     .ToListAsync();

            var aIds = allotments.Select(a => a.Id).ToList();

            var lines = await db.AllotmentRoomTypes
                                .Where(l => aIds.Contains(l.AllotmentId))
                                .Select(l => new { l.Id, l.AllotmentId, l.Quantity })
                                .ToListAsync();

            var lineIds = lines.Select(l => l.Id).ToList();

            var soldByLine = await db.ReservationLines
                .Include(ri => ri.Reservation)
                .Where(ri => lineIds.Contains(ri.AllotmentRoomTypeId) &&
                             ri.Reservation.Status != ReservationStatus.Cancelled)
                .GroupBy(ri => ri.AllotmentRoomTypeId)
                .Select(g => new { LineId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.LineId, x => x.Qty);

            var remainByAllot = aIds.ToDictionary(id => id, _ => 0);
            foreach (var l in lines)
            {
                var sold = soldByLine.TryGetValue(l.Id, out var s) ? s : 0;
                var remaining = Math.Max(0, l.Quantity - sold);
                remainByAllot[l.AllotmentId] += remaining;
            }

            Allotments.Clear();
            foreach (var a in allotments)
            {
                Allotments.Add(new AllotmentRow
                {
                    Id = a.Id,
                    HotelId = a.HotelId,
                    Title = a.Title,
                    HotelName = a.Hotel!.Name,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    OptionDueDate = a.OptionDueDate,
                    Status = a.Status,
                    RemainingTotal = remainByAllot.TryGetValue(a.Id, out var rem) ? rem : 0,
                    Notes = a.Notes
                });
            }

            if (Selected != null) RefreshLinesForSelected(); // see note below about making this async
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

            using (Busy.Begin())
            {
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

                    foreach (var l in Lines)
                    {
                        db.AllotmentRoomTypes.Add(new AllotmentRoomType
                        {
                            AllotmentId = a.Id,
                            RoomTypeId = l.RoomTypeId,
                            Quantity = l.Quantity,
                            PricePerNight = l.PricePerNight,
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
                        });
                    }
                    await db.SaveChangesAsync();
                }

                IsEditing = false;
                await LoadAsync();
            }
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

            using (Busy.Begin())
            {
                await using var db = await _dbf.CreateDbContextAsync();

                var lines = db.AllotmentRoomTypes.Where(x => x.AllotmentId == Selected.Id);
                db.AllotmentRoomTypes.RemoveRange(lines);
                db.Allotments.Remove(await db.Allotments.FirstAsync(x => x.Id == Selected.Id));
                await db.SaveChangesAsync();

                await LoadAsync();
            }
        }

        // ----- Lines commands -----

        [RelayCommand]
        private void UpsertLine()
        {
            if (LineRoomType == null) return;
            if (!int.TryParse(LineTotalQty ?? "0", out var total)) total = 0;
            if (!decimal.TryParse(LinePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                price = 0m;

            if (SelectedLine == null)
            {
                Lines.Add(new LineRow
                {
                    RoomTypeId = LineRoomType.Id,
                    RoomType = LineRoomType,
                    Quantity = total,
                    Sold = 0,
                    PricePerNight = price,
                });
            }
            else
            {
                SelectedLine.RoomTypeId = LineRoomType.Id;
                SelectedLine.RoomType = LineRoomType;
                SelectedLine.Quantity = total;
                // keep existing Cancelled & Sold
                SelectedLine.PricePerNight = price;
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

        private void ResetLineEditor()
        {
            LineRoomType = null;
            LineTotalQty = "0";
            LinePrice = "0";
            LineCancelQty = "";
            SelectedLine = null;
        }

        private void RefreshLinesForSelected()
        {
            using (Busy.Begin())
            {
                Lines.Clear();
                if (Selected == null) return;

                // Offload the DB work to keep the UI responsive and show the spinner
                Task.Run(() =>
                {
                    using var db = _dbf.CreateDbContext();

                    var soldByLine = db.ReservationLines
                        .Include(ri => ri.Reservation)
                        .Where(ri => ri.Reservation.Status != ReservationStatus.Cancelled)
                        .GroupBy(ri => ri.AllotmentRoomTypeId)
                        .Select(g => new { LineId = g.Key, Qty = g.Sum(x => x.Quantity) })
                        .ToDictionary(x => x.LineId, x => x.Qty);

                    var rows = db.AllotmentRoomTypes
                                 .Include(x => x.RoomType)
                                 .Where(x => x.AllotmentId == Selected.Id)
                                 .AsNoTracking()
                                 .ToList();

                    // back to UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var l in rows)
                        {
                            var sold = soldByLine.TryGetValue(l.Id, out var s) ? s : 0;
                            Lines.Add(new LineRow
                            {
                                Id = l.Id,
                                RoomTypeId = l.RoomTypeId,
                                RoomType = l.RoomType,
                                Quantity = l.Quantity,
                                Sold = sold,
                                PricePerNight = l.PricePerNight,
                            });
                        }
                    });
                });
            }
        }

        // lightweight row VM for the grid
        public class LineRow : ObservableObject
        {
            public int Id { get; set; }
            public int RoomTypeId { get; set; }
            public RoomType RoomType { get; set; } = null!;
            public int Quantity { get; set; }
            public int Sold { get; set; }
            public int Remaining => Math.Max(0, Quantity - Sold);
            public decimal PricePerNight { get; set; }
        }

        public class AllotmentRow : ObservableObject
        {
            public string DateRangeShort => FormatDateRange(StartDate, EndDate);

            private static string FormatDateRange(DateTime? start, DateTime? end)
            {
                if (start is null || end is null) return "";

                var s = start.Value;
                var e = end.Value;

                // If swapped by mistake, normalize
                if (e < s) (s, e) = (e, s);

                if (s.Year == e.Year)
                {
                    if (s.Month == e.Month)
                        // 10-15/07/19
                        return $"{s.Day}-{e.Day}/{e:MM/yy}";
                    else
                        // 25/07-5/08/19
                        return $"{s:d/MM}-{e:d/MM/yy}";
                }
                // 28/12/19-3/01/20
                return $"{s:d/MM/yy}-{e:d/MM/yy}";
            }

            public int Id { get; init; }
            public int HotelId { get; init; }
            public string Title { get; init; } = "";
            public string HotelName { get; init; } = "";
            public DateTime StartDate { get; init; }
            public DateTime EndDate { get; init; }
            public DateTime? OptionDueDate { get; init; }
            public AllotmentStatus Status { get; init; }
            public int RemainingTotal { get; init; }
            public string? Notes { get; init; }           // <-- add this
        }
    }
}