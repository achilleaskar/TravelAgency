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
    public partial class HotelsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public HotelsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;
        }

        // Master grid items
        public ObservableCollection<Hotel> Items { get; } = new();

        // Dropdowns from cache (preloaded at startup)
        public ObservableCollection<City> Cities => _cache.Cities;

        // Selection + filters
        [ObservableProperty] private Hotel? selected;
        [ObservableProperty] private string? searchText;

        // Editor state
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select an item for editing.";

        // Editor fields
        [ObservableProperty] private string? editName;
        [ObservableProperty] private City? editCity;
        [ObservableProperty] private string? editAddress;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editNotes;

        private bool _isNewMode;
        private int? _editingId;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(Hotel? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));

            // If user clicked "Add New" and then selects a row, flip to Edit
            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditName = value.Name;
                EditAddress = value.Address;
                EditPhone = value.Phone;
                EditEmail = value.Email;
                EditNotes = value.Notes;
                EditCity = Cities.FirstOrDefault(c => c.Id == value.CityId);

                EditorTitle = $"Edit Hotel #{value.Id}";
                EditorHint = "Change values and click Save.";
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Items.Clear();

            var q = db.Hotels.Include(h => h.City).AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(h => h.Name.Contains(SearchText) || h.City!.Name.Contains(SearchText));

            var list = await q.AsNoTracking().OrderBy(h => h.Name).ToListAsync();
            foreach (var h in list) Items.Add(h);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;

            Selected = null; // allow next row click to switch to edit

            EditName = ""; EditAddress = ""; EditPhone = ""; EditEmail = ""; EditNotes = "";
            EditCity = Cities.FirstOrDefault();

            EditorTitle = "Add New Hotel";
            EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;

            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;

            EditName = Selected.Name;
            EditAddress = Selected.Address;
            EditPhone = Selected.Phone;
            EditEmail = Selected.Email;
            EditNotes = Selected.Notes;
            EditCity = Cities.FirstOrDefault(c => c.Id == Selected.CityId);

            EditorTitle = $"Edit Hotel #{Selected.Id}";
            EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName) || EditCity == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

            if (_isNewMode)
            {
                db.Hotels.Add(new Hotel
                {
                    Name = EditName!.Trim(),
                    CityId = EditCity.Id,
                    Address = EditAddress,
                    Phone = EditPhone,
                    Email = EditEmail,
                    Notes = EditNotes
                });
            }
            else if (_editingId.HasValue)
            {
                var h = await db.Hotels.FirstAsync(x => x.Id == _editingId.Value);
                h.Name = EditName!.Trim();
                h.CityId = EditCity.Id;
                h.Address = EditAddress;
                h.Phone = EditPhone;
                h.Email = EditEmail;
                h.Notes = EditNotes;
            }

            await db.SaveChangesAsync();
            await _cache.RefreshAsync(); // keep Cities/Hotels in dropdowns current

            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;

            ResetEditorFields();

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the left list to select a hotel.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        private void ResetEditorFields()
        {
            EditName = "";
            EditAddress = "";
            EditPhone = "";
            EditEmail = "";
            EditNotes = "";
            EditCity = null;
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;

            await using var db = await _dbf.CreateDbContextAsync();

            db.Hotels.Remove(await db.Hotels.FirstAsync(x => x.Id == Selected.Id));
            await db.SaveChangesAsync();

            await _cache.RefreshAsync(); // reflect removal across app
            await LoadAsync();
        }
    }
}
