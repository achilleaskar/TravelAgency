using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class HotelsViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;

        public ObservableCollection<Hotel> Hotels { get; } = new();
        public ObservableCollection<City> Cities { get; } = new();

        [ObservableProperty]
        private Hotel? selected;
        [ObservableProperty]
        private string? searchText;

        // Editor fields
        [ObservableProperty]
        private string? editName;
        [ObservableProperty]
        private City? editCity;
        [ObservableProperty]
        private string? editAddress;
        [ObservableProperty]
        private string? editPhone;
        [ObservableProperty]
        private string? editEmail;

        // Quick city add
        [ObservableProperty]
        private string? newCityName;
        [ObservableProperty]
        private string? newCityCountry = "GR";

        public HotelsViewModel(TravelAgencyDbContext db) => _db = db;

        [RelayCommand]
        private async Task LoadAsync()
        {
            Cities.Clear();
            foreach (var c in await _db.Cities.OrderBy(x => x.Name).ToListAsync()) Cities.Add(c);

            Hotels.Clear();
            var q = _db.Hotels.Include(h => h.City).AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(h => h.Name.Contains(SearchText) || h.City!.Name.Contains(SearchText));
            foreach (var h in await q.AsNoTracking().OrderBy(h => h.Name).ToListAsync()) Hotels.Add(h);
        }

        [RelayCommand]
        private async Task AddCityAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCityName) || string.IsNullOrWhiteSpace(NewCityCountry)) return;
            if (!await _db.Cities.AnyAsync(x => x.Name == NewCityName && x.Country == NewCityCountry))
            {
                _db.Cities.Add(new City { Name = NewCityName!, Country = NewCityCountry! });
                await _db.SaveChangesAsync();
                await LoadAsync();
                EditCity = Cities.FirstOrDefault(x => x.Name == NewCityName && x.Country == NewCityCountry);
            }
        }

        partial void OnSelectedChanged(Hotel? value)
        {
            if (value == null)
            {
                EditName = EditAddress = EditPhone = EditEmail = null;
                EditCity = null;
            }
            else
            {
                EditName = value.Name;
                EditAddress = value.Address;
                EditPhone = value.Phone;
                EditEmail = value.Email;
                EditCity = Cities.FirstOrDefault(c => c.Id == value.CityId);
            }
        }

        [RelayCommand]
        private void New()
        {
            Selected = null;
            EditName = EditAddress = EditPhone = EditEmail = string.Empty;
            EditCity = Cities.FirstOrDefault();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName) || EditCity == null) return;

            if (Selected == null)
            {
                _db.Hotels.Add(new Hotel
                {
                    Name = EditName!.Trim(),
                    CityId = EditCity.Id,
                    Address = EditAddress,
                    Phone = EditPhone,
                    Email = EditEmail
                });
            }
            else
            {
                var h = await _db.Hotels.FirstAsync(x => x.Id == Selected.Id);
                h.Name = EditName!.Trim();
                h.CityId = EditCity.Id;
                h.Address = EditAddress;
                h.Phone = EditPhone;
                h.Email = EditEmail;
            }

            await _db.SaveChangesAsync();
            await LoadAsync();
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;
            var h = await _db.Hotels.FirstAsync(x => x.Id == Selected.Id);
            _db.Hotels.Remove(h);
            await _db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
