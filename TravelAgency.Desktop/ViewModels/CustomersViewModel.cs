using System.Collections.ObjectModel;
using System.Globalization;
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
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        public CustomersViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;
        }

        // Master grid
        public ObservableCollection<Customer> Items { get; } = new();

        // Selection + search
        [ObservableProperty] private Customer? selected;
        [ObservableProperty] private string? searchText;

        // Editor state
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select a customer.";

        // Editor fields
        [ObservableProperty] private string? editName;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editOldBalance = "0";
        [ObservableProperty] private string? editNotes;

        private bool _isNewMode;
        private int? _editingId;

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(Customer? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));

            // If user started "Add New" then clicked a row, flip to Edit
            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditName = value.Name;
                EditEmail = value.Email;
                EditPhone = value.Phone;
                EditOldBalance = value.OldBalance.ToString("0.##", CultureInfo.InvariantCulture);
                EditNotes = value.Notes;

                EditorTitle = $"Edit Customer #{value.Id}";
                EditorHint = "Change values and click Save.";
            }
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Items.Clear();

            var q = db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(c => c.Name.Contains(SearchText) ||
                                 (c.Email != null && c.Email.Contains(SearchText)) ||
                                 (c.Phone != null && c.Phone.Contains(SearchText)));

            var list = await q.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            foreach (var c in list) Items.Add(c);
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true; _editingId = null; IsEditing = true;

            Selected = null; // allow next row click to switch to edit

            EditName = ""; EditEmail = ""; EditPhone = ""; EditOldBalance = "0"; EditNotes = "";

            EditorTitle = "Add New Customer";
            EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;

            _isNewMode = false; _editingId = Selected.Id; IsEditing = true;

            EditName = Selected.Name;
            EditEmail = Selected.Email;
            EditPhone = Selected.Phone;
            EditOldBalance = Selected.OldBalance.ToString("0.##", CultureInfo.InvariantCulture);
            EditNotes = Selected.Notes;

            EditorTitle = $"Edit Customer #{Selected.Id}";
            EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName)) return;

            await using var db = await _dbf.CreateDbContextAsync();

            var ok = decimal.TryParse(EditOldBalance ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var oldBal);
            if (!ok) oldBal = 0m;

            if (_isNewMode)
            {
                db.Customers.Add(new Customer
                {
                    Name = EditName!.Trim(),
                    Email = EditEmail,
                    Phone = EditPhone,
                    OldBalance = oldBal,
                    Notes = EditNotes
                });
            }
            else if (_editingId.HasValue)
            {
                var c = await db.Customers.FirstAsync(x => x.Id == _editingId.Value);
                c.Name = EditName!.Trim();
                c.Email = EditEmail;
                c.Phone = EditPhone;
                c.OldBalance = oldBal;
                c.Notes = EditNotes;
            }

            await db.SaveChangesAsync();
            await _cache.RefreshAsync(); // keep dashboard filters, etc. in sync

            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false; _isNewMode = false; _editingId = null;

            EditName = ""; EditEmail = ""; EditPhone = ""; EditOldBalance = "0"; EditNotes = "";

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the left list to select a customer.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;

            await using var db = await _dbf.CreateDbContextAsync();
            db.Customers.Remove(await db.Customers.FirstAsync(x => x.Id == Selected.Id));
            await db.SaveChangesAsync();

            await _cache.RefreshAsync(); // reflect removal across app
            await LoadAsync();
        }
    }
}
