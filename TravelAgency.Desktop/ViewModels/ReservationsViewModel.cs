using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;
using TravelAgency.Services;

public partial class ReservationsViewModel : ObservableObject
{
    private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
    private readonly LookupCacheService _cache;

    // filters from cache
    public ObservableCollection<City> Cities => _cache.Cities;
    public ObservableCollection<Customer> Customers => _cache.Customers;

    // left filter area
    [ObservableProperty] private City? selectedCity;
    [ObservableProperty] private DateTime fromDate = DateTime.Today;
    [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(30);

    // right composer
    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private string? title;
    [ObservableProperty] private DateTime? resFrom = DateTime.Today;
    [ObservableProperty] private DateTime? resTo = DateTime.Today.AddDays(3);
    [ObservableProperty] private DateTime? depositDue;
    [ObservableProperty] private DateTime? balanceDue;
    [ObservableProperty] private string? notes;

    public ObservableCollection<AvailableAllotmentDto> Available { get; } = new();
    public ObservableCollection<ReservationBasketLine> Basket { get; } = new();

    public ReservationsViewModel(IDbContextFactory<TravelAgencyDbContext> dbf, LookupCacheService cache)
    {
        _dbf = dbf;
        _cache = cache;
    }

    [RelayCommand]
    private async Task LoadAvailableAsync()
    {
        await using var db = await _dbf.CreateDbContextAsync();

        // query allotments by city + date range
        var q = db.Allotments
                  .Include(a => a.Hotel)
                  .ThenInclude(h => h.City)
                  .AsQueryable();

        if (SelectedCity != null)
            q = q.Where(a => a.Hotel!.CityId == SelectedCity.Id);

        q = q.Where(a => a.EndDate >= FromDate && a.StartDate <= ToDate);

        var list = await q.AsNoTracking()
                          .OrderBy(a => a.StartDate)
                          .Select(a => new AvailableAllotmentDto
                          {
                              AllotmentId = a.Id,
                              Title = a.Title,
                              HotelName = a.Hotel!.Name,
                              StartDate = a.StartDate,
                              EndDate = a.EndDate
                          })
                          .ToListAsync();

        Available.Clear();
        foreach (var it in list) Available.Add(it);
    }

    [RelayCommand]
    private void AddServiceLine()
    {
        if (string.IsNullOrWhiteSpace(ServiceName)) return;
        if (!int.TryParse(ServiceQty ?? "1", out var qty)) qty = 1;
        if (!decimal.TryParse(ServicePrice ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            price = 0m;

        Basket.Add(new ReservationBasketLine
        {
            Kind = "Service",
            Title = ServiceName!.Trim(),
            Qty = qty,
            UnitPrice = price,
            Currency = string.IsNullOrWhiteSpace(ServiceCurrency) ? "EUR" : ServiceCurrency!
        });

        ServiceName = ""; ServiceQty = "1"; ServicePrice = "0"; ServiceCurrency = "EUR";
    }

    // service subform fields
    [ObservableProperty] private string? ServiceName;
    [ObservableProperty] private string? ServiceQty = "1";
    [ObservableProperty] private string? ServicePrice = "0";
    [ObservableProperty] private string? ServiceCurrency = "EUR";

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedCustomer == null || string.IsNullOrWhiteSpace(Title) || ResFrom == null || ResTo == null) return;

        await using var db = await _dbf.CreateDbContextAsync();

        var r = new Reservation
        {
            CustomerId = SelectedCustomer.Id,
            Title = Title!.Trim(),
            StartDate = ResFrom!.Value.Date,
            EndDate = ResTo!.Value.Date,
            DepositDueDate = DepositDue,
            BalanceDueDate = BalanceDue,
            Notes = Notes
            // Status: leave default (your enum excludes 'Active')
        };
        db.Reservations.Add(r);
        await db.SaveChangesAsync();

        foreach (var line in Basket)
        {
            if (line.Kind == "AllotmentRoom")
            {
                db.ReservationItems.Add(new ReservationItem
                {
                    ReservationId = r.Id,
                    Kind = ReservationItemKind.AllotmentRoom,
                    AllotmentRoomTypeId = line.AllotmentRoomTypeId,
                    Qty = line.Qty,
                    UnitPrice = line.UnitPrice,
                    Currency = line.Currency,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate
                });
            }
            else
            {
                db.ReservationItems.Add(new ReservationItem
                {
                    ReservationId = r.Id,
                    Kind = ReservationItemKind.Service,
                    ServiceName = line.Title,
                    Qty = line.Qty,
                    UnitPrice = line.UnitPrice,
                    Currency = line.Currency
                });
            }
        }
        await db.SaveChangesAsync();

        // reset composer
        Title = ""; Notes = "";
        Basket.Clear();
    }
}
