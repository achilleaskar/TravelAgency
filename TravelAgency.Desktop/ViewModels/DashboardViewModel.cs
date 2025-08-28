using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;                           // <-- for Application.Current.Dispatcher
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly AlertService _alerts;
    private readonly LookupCacheService _cache;

    public ObservableCollection<Customer> Customers => _cache.Customers;
    public ObservableCollection<Hotel> Hotels => _cache.Hotels;
    public ObservableCollection<string> Countries { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private Hotel? selectedHotel;
    [ObservableProperty] private string? selectedCountry;
    [ObservableProperty] private string? searchText;

    public ObservableCollection<AlertDto> Alerts { get; } = new();

    public DashboardViewModel(AlertService alerts, LookupCacheService cache)
    {
        _alerts = alerts;
        _cache = cache;

        RebuildCountries();

        // auto-refresh Countries when Cities change
        _cache.Refreshed += OnCacheRefreshed;

        _ = RefreshAsync();
    }

    private void OnCacheRefreshed(object? sender, EventArgs e)
    {
        void Work()
        {
            // Preserve current selection if still present
            var prev = SelectedCountry;
            RebuildCountries();
            if (prev != null && Countries.Contains(prev)) SelectedCountry = prev;
        }

        var disp = Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess()) disp.Invoke(Work);
        else Work();
    }

    private void RebuildCountries()
    {
        Countries.Clear();
        foreach (var c in _cache.Cities
                                .Select(x => x.Country)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct()
                                .OrderBy(x => x))
        {
            Countries.Add(c);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Alerts.Clear();

        var list = await _alerts.GetAlertsAsync(
            today: DateTime.Today,
            hotelId: SelectedHotel?.Id,
            country: SelectedCountry,
            customerId: SelectedCustomer?.Id,
            search: SearchText);

        foreach (var a in list)
            Alerts.Add(a);
    }


    public void Dispose()
    {
        _cache.Refreshed -= OnCacheRefreshed;
    }
}
