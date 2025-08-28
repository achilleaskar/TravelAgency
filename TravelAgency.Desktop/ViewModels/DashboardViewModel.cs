using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
    private readonly LookupCacheService _cache;

    public ObservableCollection<Customer> Customers => _cache.Customers;
    public ObservableCollection<Hotel> Hotels => _cache.Hotels;
    public ObservableCollection<string> Countries { get; } = new();

    public DashboardViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
    {
        _dbf = dbf; _cache = cache;

        // when cache loads (startup or refresh) rebuild countries and load alerts
        _cache.Refreshed += (_, __) =>
        {
            Countries.Clear();
            foreach (var c in _cache.Cities.Select(c => c.Country).Distinct().OrderBy(x => x))
                Countries.Add(c);

            // optional: pick defaults
            SelectedCustomer ??= Customers.FirstOrDefault();
            SelectedHotel ??= Hotels.FirstOrDefault();

            _ = LoadAlertsAsync();
        };
    }

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private Hotel? selectedHotel;
    [ObservableProperty] private string? selectedCountry;
    [ObservableProperty] private string? searchText;

    public ObservableCollection<AlertDto> Alerts { get; } = new();

    [RelayCommand]
    private async Task LoadAlertsAsync()
    {
        var today = DateTime.Today;
        var hotelId = SelectedHotel?.Id;
        var country = string.IsNullOrWhiteSpace(SelectedCountry) ? null : SelectedCountry;
        var custId = SelectedCustomer?.Id;
        var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

        var svc = new AlertService(_dbf);
        var list = await svc.GetAlertsAsync(today, hotelId, country, custId, search);

        Alerts.Clear();
        foreach (var a in list) Alerts.Add(a);
    }
}
