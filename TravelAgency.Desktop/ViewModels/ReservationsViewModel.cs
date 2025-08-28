using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using TravelAgency.Data;
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

    [ObservableProperty] private Customer? selectedCustomer;
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
                  .Where(r => r.CustomerId == SelectedCustomer.Id &&
                              r.StartDate <= ToDate &&
                              r.EndDate >= FromDate)
                  .OrderByDescending(r => r.StartDate)
                  .AsNoTracking();

        foreach (var r in await q.ToListAsync()) Reservations.Add(r);
    }

    // Add New
    [RelayCommand]
    private void AddNew()
    {
        if (SelectedCustomer == null) return;

        var sp = App.HostRef.Services; // your ServiceProvider
                                       // DI will inject registered services; we supply the primitives:
        var vm = ActivatorUtilities.CreateInstance<ReservationEditorViewModel>(
            sp,
            SelectedCustomer.Id, // customerId (int)
            null                 // reservationId (int?)
        );

        var win = new ReservationEditorWindow(vm) { Owner = Application.Current.MainWindow };
        win.ShowDialog();

        LoadReservationsCommand.Execute(null);
    }

    // Edit
    [RelayCommand]
    private void EditSelected()
    {
        if (Selected == null) return;

        var sp = App.HostRef.Services;
        var vm = ActivatorUtilities.CreateInstance<ReservationEditorViewModel>(
            sp,
            Selected.CustomerId, // customerId
            (int?)Selected.Id    // reservationId
        );

        var win = new ReservationEditorWindow(vm) { Owner = Application.Current.MainWindow };
        win.ShowDialog();

        LoadReservationsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task DeleteSelected()
    {
        if (Selected == null) return;
        await using var db = await _dbf.CreateDbContextAsync();
        var r = await db.Reservations.Include(x => x.Items).FirstAsync(x => x.Id == Selected.Id);
        db.ReservationItems.RemoveRange(r.Items);
        db.Reservations.Remove(r);
        await db.SaveChangesAsync();
        await LoadReservations();
    }
}
