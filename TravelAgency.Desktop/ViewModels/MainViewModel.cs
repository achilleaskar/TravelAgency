using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TravelAgency.Services;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<AlertDto> Alerts { get; } = new();

    private readonly AlertService _alerts;
    public MainViewModel(AlertService alerts)
    {
        _alerts = alerts;
        _ = RefreshAsync();
    }

    [ObservableProperty] 
    private DateTime? filterStart = DateTime.Today;
    [ObservableProperty] 
    private DateTime? filterEnd = DateTime.Today.AddDays(30);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Alerts.Clear();
        var list = await _alerts.GetAlertsAsync(DateTime.Today);
        foreach (var a in list) Alerts.Add(a);
    }
}