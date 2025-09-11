using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class CitiesViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public ObservableCollection<City> Items { get; } = new();

        [ObservableProperty] private City? selected;
        [ObservableProperty] private string? searchText;

        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the list on the left to select a city.";

        [ObservableProperty] private string? editName;
        [ObservableProperty] private string? editCountry;

        private bool _isNewMode;
        private int? _editingId;

        public CitiesViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;
        }

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(City? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));

            // If user started "Add New" and then clicked a row, auto-switch to Edit
            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditName = value.Name;
                EditCountry = value.Country;

                EditorTitle = $"Edit City #{value.Id}";
                EditorHint = "Change values and click Save.";
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Items.Clear();
            var q = db.Cities.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(x => x.Name.Contains(SearchText) || x.Country.Contains(SearchText));

            foreach (var it in await q.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
                Items.Add(it);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;

            Selected = null; // allow next row click to switch to edit

            EditName = "";
            EditCountry = "";

            EditorTitle = "Add New City";
            EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;

            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;

            EditName = Selected.Name;
            EditCountry = Selected.Country;

            EditorTitle = $"Edit City #{Selected.Id}";
            EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditCountry)) return;

            await using var db = await _dbf.CreateDbContextAsync();

            if (_isNewMode)
            {
                db.Cities.Add(new City { Name = EditName!.Trim(), Country = EditCountry!.Trim() });
            }
            else if (_editingId.HasValue)
            {
                var entity = await db.Cities.FirstAsync(x => x.Id == _editingId.Value);
                entity.Name = EditName!.Trim();
                entity.Country = EditCountry!.Trim();
            }

            await db.SaveChangesAsync();
            await _cache.RefreshAsync();   // <— keep Hotels/filters dropdowns in sync

            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;

            EditName = ""; EditCountry = "";

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the list on the left to select a city.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;

            await using var db = await _dbf.CreateDbContextAsync();
            var entity = await db.Cities.FirstAsync(x => x.Id == Selected.Id);
            db.Cities.Remove(entity);
            await db.SaveChangesAsync();

            await _cache.RefreshAsync();   // <— reflect removal across app
            await LoadAsync();
        }
    }
}
