using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.ObjectModel;
using System.Globalization;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        public CustomersViewModel(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

        public ObservableCollection<Customer> Items { get; } = new();

        [ObservableProperty] private Customer? selected;
        [ObservableProperty] private string? searchText;
        [ObservableProperty] private string? editNotes;

        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select an item for editing.";

        [ObservableProperty] private string? editName;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editOldBalance;

        private bool _isNewMode;
        private int? _editingId;
         

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;
        partial void OnSelectedChanged(Customer? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));

            if (value != null && IsEditing && _isNewMode)
            {
                _isNewMode = false;
                _editingId = value.Id;

                EditName = value.Name;
                EditEmail = value.Email;
                EditPhone = value.Phone;
                EditOldBalance = value.OldBalance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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
                q = q.Where(c => c.Name.Contains(SearchText) || (c.Email ?? "").Contains(SearchText));
            foreach (var c in await q.AsNoTracking().OrderBy(c => c.Name).ToListAsync()) Items.Add(c);
        }
        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true;
            _editingId = null;
            IsEditing = true;

            Selected = null;

            EditName = "";
            EditEmail = "";
            EditPhone = "";
            EditOldBalance = "0";
            EditNotes = "";

            EditorTitle = "Add New Customer";
            EditorHint = "Fill the fields and click Save.";
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
            await using var db = await _dbf.CreateDbContextAsync();

            if (string.IsNullOrWhiteSpace(EditName)) return;
            if (!decimal.TryParse(EditOldBalance ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var ob)) ob = 0;

            if (_isNewMode)
            {
                db.Customers.Add(new Customer
                {
                    Name = EditName!.Trim(),
                    Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim(),
                    OldBalance = ob,
                    Notes = EditNotes
                });
            }
            else if (_editingId.HasValue)
            {
                var c = await db.Customers.FirstAsync(x => x.Id == _editingId.Value);
                c.Name = EditName!.Trim();
                c.Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim();
                c.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim();
                c.OldBalance = ob;
                c.Notes = EditNotes;
            }

            await db.SaveChangesAsync();
            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false;
            _isNewMode = false;
            _editingId = null;

            ResetEditorFields();

            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the list on the left to select a customer.";

            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        private void ResetEditorFields()
        {
            EditName = "";
            EditEmail = "";
            EditPhone = "";
            EditOldBalance = "0";
            EditNotes = "";
        }


        [RelayCommand]
        private async Task DeleteAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (Selected == null) return;
            var c = await db.Customers.FirstAsync(x => x.Id == Selected.Id);
            db.Customers.Remove(c);
            await db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
