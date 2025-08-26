using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

public partial class DashboardViewModel : ObservableObject
{
    private readonly AlertService _alerts;
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf; 

    public ObservableCollection<AlertDto> Alerts { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Hotel> Hotels { get; } = new();
    public ObservableCollection<string> Countries { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private Hotel? selectedHotel;
    [ObservableProperty] private string? selectedCountry;
    [ObservableProperty] private string? searchText;

    public DashboardViewModel(AlertService alerts, IDbContextFactory<TravelAgencyDbContext> dbf)
    {
        _alerts = alerts;
        _dbf = dbf;
        _ = LoadFiltersAsync();
        _ = RefreshAsync();
    }

    private async Task LoadFiltersAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();

        Customers.Clear();
        foreach (var c in await db.Customers.OrderBy(x => x.Name).ToListAsync()) Customers.Add(c);

        Hotels.Clear();
        foreach (var h in await db.Hotels.OrderBy(x => x.Name).ToListAsync()) Hotels.Add(h);

        Countries.Clear();
        foreach (var country in await db.Cities.Select(x => x.Country).Distinct().OrderBy(x => x).ToListAsync())
            Countries.Add(country);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Alerts.Clear();
        var list = await _alerts.GetAlertsAsync(
            DateTime.Today,
            hotelId: SelectedHotel?.Id,
            country: SelectedCountry,
            customerId: SelectedCustomer?.Id,
            search: SearchText
        );
        foreach (var a in list) Alerts.Add(a);
    }
}
