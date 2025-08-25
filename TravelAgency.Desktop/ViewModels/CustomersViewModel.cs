using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;
        public ObservableCollection<Customer> Customers { get; } = new();

        [ObservableProperty] private Customer? selected;
        [ObservableProperty] private string? searchText;

        [ObservableProperty] private string? editName;
        [ObservableProperty] private string? editEmail;
        [ObservableProperty] private string? editPhone;
        [ObservableProperty] private string? editOldBalance; // as text for easy binding

        public CustomersViewModel(TravelAgencyDbContext db) => _db = db;

        [RelayCommand]
        private async Task LoadAsync()
        {
            Customers.Clear();
            var q = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(c => c.Name.Contains(SearchText) || c.Email!.Contains(SearchText));
            foreach (var c in await q.AsNoTracking().OrderBy(c => c.Name).ToListAsync())
                Customers.Add(c);
        }

        partial void OnSelectedChanged(Customer? value)
        {
            if (value == null)
            {
                EditName = EditEmail = EditPhone = EditOldBalance = null;
            }
            else
            {
                EditName = value.Name;
                EditEmail = value.Email;
                EditPhone = value.Phone;
                EditOldBalance = value.OldBalance.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        [RelayCommand]
        private void New()
        {
            Selected = null;
            EditName = EditEmail = EditPhone = string.Empty;
            EditOldBalance = "0";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName)) return;
            if (!decimal.TryParse(EditOldBalance ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var ob)) ob = 0;

            if (Selected == null)
            {
                _db.Customers.Add(new Customer
                {
                    Name = EditName!.Trim(),
                    Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim(),
                    OldBalance = ob
                });
            }
            else
            {
                var c = await _db.Customers.FirstAsync(x => x.Id == Selected.Id);
                c.Name = EditName!.Trim();
                c.Email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail!.Trim();
                c.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone!.Trim();
                c.OldBalance = ob;
            }

            await _db.SaveChangesAsync();
            await LoadAsync();
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
