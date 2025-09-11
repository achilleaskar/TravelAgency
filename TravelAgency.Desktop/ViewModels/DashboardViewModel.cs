using System.Collections.ObjectModel;
using System.Windows;                 // Application.Current.Dispatcher
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        private readonly LookupCacheService _cache;

        // bind to the *shared* cache collections (DON’T copy into new ObservableCollections)
        
        public ObservableCollection<Customer> Customers => _cache.Customers;

        public ObservableCollection<Hotel> Hotels => _cache.Hotels;

        // Countries are derived, so we keep a local list and rebuild on cache refresh
        public ObservableCollection<string> Countries { get; } = new();

        [ObservableProperty] private Customer? selectedCustomer;
        [ObservableProperty] private Hotel? selectedHotel;
        [ObservableProperty] private string? selectedCountry;
        [ObservableProperty] private string? searchText;

        public ObservableCollection<AlertDto> Alerts { get; } = new();

        public DashboardViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
        {
            _dbf = dbf;
            _cache = cache;

            // whenever cache refreshes (startup or later), rebuild filters and load alerts
            _cache.Refreshed += (_, __) => ApplyCache();

            // if cache is already loaded by the time this VM is created, apply immediately
            if (_cache.Cities.Count > 0 || _cache.Customers.Count > 0) ApplyCache();
        }

        private void ApplyCache()
        {
            void Do()
            {
                Countries.Clear();
                foreach (var c in _cache.Cities
                                         .Select(ci => ci.Country)
                                         .Where(s => !string.IsNullOrWhiteSpace(s))
                                         .Distinct()
                                         .OrderBy(s => s))
                    Countries.Add(c);

                // pick sane defaults once
                SelectedCustomer ??= Customers.FirstOrDefault();
                SelectedHotel ??= Hotels.FirstOrDefault();

                // nudge bindings (rarely needed, but harmless)
                OnPropertyChanged(nameof(Customers));
                OnPropertyChanged(nameof(Hotels));

                _ = LoadAlertsAsync();
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(Do);
            else Do();
        }

        [RelayCommand]
        private async Task LoadAlertsAsync()
        {
            var svc = new AlertService(_dbf);

            var list = await svc.GetAlertsAsync(
                DateTime.Today,
                SelectedHotel?.Id,
                string.IsNullOrWhiteSpace(SelectedCountry) ? null : SelectedCountry,
                SelectedCustomer?.Id,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);

            Alerts.Clear();
            foreach (var a in list) Alerts.Add(a);
        }
    }
}
