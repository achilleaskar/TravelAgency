// ... usings same as before
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class HotelsViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;

        public ObservableCollection<Hotel> Items { get; } = new();
        public ObservableCollection<City> Cities { get; } = new();

        [ObservableProperty] private Hotel? selected;
        [ObservableProperty] private string? searchText;

        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select an item for editing.";

        [ObservableProperty] private string? editName;
        [ObservableProperty] private City? editCity;
        [ObservableProperty] private string? editAddress;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editNotes;

        private bool _isNewMode;
        private int? _editingId;

        public HotelsViewModel(TravelAgencyDbContext db) => _db = db;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;
        partial void OnSelectedChanged(Hotel? value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanDelete)); }

        [RelayCommand]
        private async Task LoadAsync()
        {
            Cities.Clear();
            foreach (var c in await _db.Cities.OrderBy(x => x.Name).ToListAsync()) Cities.Add(c);

            Items.Clear();
            var q = _db.Hotels.Include(h => h.City).AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(h => h.Name.Contains(SearchText) || h.City!.Name.Contains(SearchText));
            foreach (var h in await q.AsNoTracking().OrderBy(h => h.Name).ToListAsync()) Items.Add(h);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;
            EditName = ""; EditAddress = ""; EditPhone = ""; EditEmail = ""; EditNotes = "";
            EditCity = Cities.FirstOrDefault();
            EditorTitle = "Add New Hotel"; EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;
            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;
            EditName = Selected.Name; EditAddress = Selected.Address; EditPhone = Selected.Phone; EditEmail = Selected.Email; EditNotes = Selected.Notes;
            EditCity = Cities.FirstOrDefault(c => c.Id == Selected.CityId);
            EditorTitle = $"Edit Hotel #{Selected.Id}"; EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName) || EditCity == null) return;

            if (_isNewMode)
            {
                _db.Hotels.Add(new Hotel
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
                var h = await _db.Hotels.FirstAsync(x => x.Id == _editingId.Value);
                h.Name = EditName!.Trim(); h.CityId = EditCity.Id;
                h.Address = EditAddress; h.Phone = EditPhone; h.Email = EditEmail; h.Notes = EditNotes;
            }

            await _db.SaveChangesAsync();
            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;
            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the left list to select an item for editing.";
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;
            _db.Hotels.Remove(await _db.Hotels.FirstAsync(x => x.Id == Selected.Id));
            await _db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
