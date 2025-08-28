using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class RoomTypesViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public RoomTypesViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;
        }

        // Master grid
        public ObservableCollection<RoomType> Items { get; } = new();

        // Selection + search
        [ObservableProperty] private RoomType? selected;
        [ObservableProperty] private string? searchText;

        // Editor state
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select a room type.";

        // Editor fields
        [ObservableProperty] private string? editCode;
        [ObservableProperty] private string? editName;

        private bool _isNewMode;
        private int? _editingId;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(RoomType? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));

            // If user started "Add New" then clicked a row, flip to Edit
            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditCode = value.Code;
                EditName = value.Name;

                EditorTitle = $"Edit Room Type #{value.Id}";
                EditorHint = "Change values and click Save.";
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Items.Clear();

            var q = db.RoomTypes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(r => r.Code.Contains(SearchText) || r.Name.Contains(SearchText));

            var list = await q.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
            foreach (var r in list) Items.Add(r);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;

            Selected = null; // allow next row click to switch to edit

            EditCode = "";
            EditName = "";

            EditorTitle = "Add New Room Type";
            EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;

            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;

            EditCode = Selected.Code;
            EditName = Selected.Name;

            EditorTitle = $"Edit Room Type #{Selected.Id}";
            EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName)) return;

            await using var db = await _dbf.CreateDbContextAsync();

            if (_isNewMode)
            {
                db.RoomTypes.Add(new RoomType
                {
                    Code = EditCode,
                    Name = EditName!.Trim()
                });
            }
            else if (_editingId.HasValue)
            {
                var rt = await db.RoomTypes.FirstAsync(x => x.Id == _editingId.Value);
                rt.Code = EditCode;
                rt.Name = EditName!.Trim();
            }

            await db.SaveChangesAsync();
            await _cache.RefreshAsync(); // Allotments lines dropdowns need this

            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;

            EditCode = ""; EditName = "";

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the left list to select a room type.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;

            await using var db = await _dbf.CreateDbContextAsync();
            db.RoomTypes.Remove(await db.RoomTypes.FirstAsync(x => x.Id == Selected.Id));
            await db.SaveChangesAsync();

            await _cache.RefreshAsync(); // reflect removal across app
            await LoadAsync();
        }
    }
}
