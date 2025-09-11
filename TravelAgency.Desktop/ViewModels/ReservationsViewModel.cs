using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Data;
using TravelAgency.Desktop.Helpers;
using TravelAgency.Desktop.Views;
using TravelAgency.Domain.Entities;
using TravelAgency.Services;

namespace TravelAgency.Desktop.ViewModels;

public partial class ReservationsViewModel : ObservableObject
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
    private readonly LookupCacheService _cache;

    public ObservableCollection<Customer> Customers => _cache.Customers;

    public ObservableCollection<Reservation> Reservations { get; } = new();

    [ObservableProperty]
    private Customer? selectedCustomer;
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddMonths(-3);
    [ObservableProperty] private DateTime toDate = DateTime.Today.AddMonths(3);
    [ObservableProperty] private Reservation? selected;

    public ReservationsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
    { _dbf = dbf; _cache = cache; }

    partial void OnSelectedCustomerChanged(Customer? value) => LoadReservationsCommand.Execute(null);

    partial void OnFromDateChanged(DateTime value) => LoadReservationsCommand.Execute(null);

    partial void OnToDateChanged(DateTime value) => LoadReservationsCommand.Execute(null);

    [RelayCommand]
    private async Task LoadReservations()
    {
        Reservations.Clear();
        if (SelectedCustomer == null) return;

        await using var db = await _dbf.CreateDbContextAsync();
        var q = db.Reservations
                        .Include(r => r.Customer)
                        .Include(r => r.Lines)
                            .ThenInclude(l => l.AllotmentRoomType)
                                .ThenInclude(art => art.Allotment)
                                    .ThenInclude(a => a.Hotel)
                        .Include(r => r.Payments);
    

        foreach (var r in await q.ToListAsync()) Reservations.Add(r);
    }

    [RelayCommand]
    private async Task AddNew()
    {
        var sp = App.HostRef!.Services;

        // VM comes from DI (services.AddTransient<ReservationEditorViewModel>())
        var vm = sp.GetRequiredService<ReservationEditorViewModel>();

        // Create the window and attach the VM
        var win = new ReservationEditorWindow(vm)
        {
            Owner = Application.Current.MainWindow
        };

        // Initialize VM (and show busy spinner while loading)
        await Busy.RunAsync(() => vm.InitializeAsNewForCustomerAsync());

        win.ShowDialog();

        // refresh after close
        await LoadReservations();
    }


    // EditSelected (caller stays the same)
    [RelayCommand]
    private void EditSelected()
    {
        if (Selected == null) return;

        var sp = App.HostRef.Services;
        var vm = sp.GetRequiredService<ReservationEditorViewModel>();

        var win = new ReservationEditorWindow(vm) { Owner = Application.Current.MainWindow };
        // Initialize via Loaded (see below)
        win.ShowDialog();

        LoadReservationsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (Selected == null) return;
        await using TravelAgencyDbContext? db = await _dbf.CreateDbContextAsync();
        var r = await db.Reservations.Include(x => x.Lines).FirstAsync(x => x.Id == Selected.Id);
        db.ReservationLines.RemoveRange(r.Lines);
        db.Reservations.Remove(r);
        await db.SaveChangesAsync();
        await LoadReservations();
    }
}