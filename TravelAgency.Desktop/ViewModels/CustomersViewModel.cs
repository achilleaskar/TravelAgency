using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;

        public ObservableCollection<Customer> Items { get; } = new();

        [ObservableProperty] private Customer? selected;
        [ObservableProperty] private string? searchText;

        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select an item for editing.";

        [ObservableProperty] private string? editName;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editOldBalance;

        private bool _isNewMode;
        private int? _editingId;

        public CustomersViewModel(TravelAgencyDbContext db) => _db = db;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;
        partial void OnSelectedChanged(Customer? value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanDelete)); }

        [RelayCommand]
        private async Task LoadAsync()
        {
            Items.Clear();
            var q = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(c => c.Name.Contains(SearchText) || (c.Email ?? "").Contains(SearchText));
            foreach (var c in await q.AsNoTracking().OrderBy(c => c.Name).ToListAsync()) Items.Add(c);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;
            EditName = ""; EditEmail = ""; EditPhone = ""; EditOldBalance = "0";
            EditorTitle = "Add New Customer"; EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;
            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;
            EditName = Selected.Name; EditEmail = Selected.Email; EditPhone = Selected.Phone;
            EditOldBalance = Selected.OldBalance.ToString("0.##", CultureInfo.InvariantCulture);
            EditorTitle = $"Edit Customer #{Selected.Id}"; EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName)) return;
            if (!decimal.TryParse(EditOldBalance ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var ob)) ob = 0;

            if (_isNewMode)
            {
                _db.Customers.Add(new Customer
                {
                    Name = EditName!.Trim(),
                    Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim(),
                    OldBalance = ob
                });
            }
            else if (_editingId.HasValue)
            {
                var c = await _db.Customers.FirstAsync(x => x.Id == _editingId.Value);
                c.Name = EditName!.Trim();
                c.Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim();
                c.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim();
                c.OldBalance = ob;
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
            var c = await _db.Customers.FirstAsync(x => x.Id == Selected.Id);
            _db.Customers.Remove(c);
            await _db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
