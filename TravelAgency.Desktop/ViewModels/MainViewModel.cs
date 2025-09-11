using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TravelAgency.Desktop.Helpers;
using TravelAgency.Services;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(AlertService alerts)
    {
        _alerts = alerts;

        // Listen for global busy changes
        WeakReferenceMessenger.Default.Register<BusyMessage>(this, (_, m) =>
        {
            IsBusy = m.Value;
        });

        // Startup load under Busy
        _ = Busy.RunAsync(RefreshAsync);
    }

    private readonly AlertService _alerts;

    [ObservableProperty] private DateTime? filterStart = DateTime.Today;
    [ObservableProperty] private DateTime? filterEnd = DateTime.Today.AddDays(30);
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var today = DateTime.Today;
        var list = await _alerts.GetAlertsAsync(today);
        Alerts.Clear();
        foreach (var a in list) Alerts.Add(a);
    }

    public ObservableCollection<AlertDto> Alerts { get; } = new();
}
