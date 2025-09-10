using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TravelAgency.Data;
using TravelAgency.Desktop.Converters;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.ViewModels
{
    // Προσοχή: χρειάζεται το package CommunityToolkit.Mvvm στο Desktop project
    public partial class PlanViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;

        public ObservableCollection<City> Cities { get; } = new();
        public ObservableCollection<string> DayHeaders { get; } = new();
        public ObservableCollection<PlanRowVM> Rows { get; } = new();

        [ObservableProperty] private City? selectedCity;
        [ObservableProperty] private DateTime fromDate = DateTime.Today;
        [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(14);
        [ObservableProperty] private string? searchText;

        public PlanViewModel(IDbContextFactory<TravelAgencyDbContext> dbf)
        {
            _dbf = dbf;
            _ = LoadCitiesAsync();
            BuildHeaders();
        }

        // Τα παρακάτω partial καλούνται αυτόματα από το source generator όταν αλλάξουν οι ιδιότητες
        partial void OnToDateChanged(DateTime value) => BuildHeaders();

        private async Task LoadCitiesAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Cities.Clear();
            var list = await db.Cities.OrderBy(x => x.Name).ToListAsync();
            foreach (var c in list) Cities.Add(c);
        }

        private void BuildHeaders()
        {
            DayHeaders.Clear();
            var start = FromDate.Date;
            var end = ToDate.Date;
            for (var d = start; d <= end; d = d.AddDays(1))
                DayHeaders.Add(d.ToString("dd/MM"));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Rows.Clear();
            DayHeaders.Clear();

            var rangeStart = FromDate.Date;
            var rangeEndEx = ToDate.Date.AddDays(1); // exclusive

            // build headers once
            for (var d = rangeStart; d < rangeEndEx; d = d.AddDays(1))
                DayHeaders.Add(d.ToString("dd/MM"));

            // fetch allotments + room types in range
            var q = db.Allotments
                      .Include(a => a.Hotel)!.ThenInclude(h => h.City)
                      .Include(a => a.RoomTypes)!.ThenInclude(rt => rt.RoomType)
                      .Where(a => a.StartDate < rangeEndEx && a.EndDate > rangeStart);

            if (SelectedCity != null)
                q = q.Where(a => a.Hotel!.CityId == SelectedCity.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(a => a.Hotel!.Name.Contains(SearchText) || a.Title.Contains(SearchText));

            var allotments = await q.AsNoTracking().ToListAsync();
            if (allotments.Count == 0) return;

            // prefetch reservation items intersecting the window
            var artList = allotments.SelectMany(a => a.RoomTypes).ToList();
            var artIds = artList.Select(x => x.Id).ToList();

            var items = await db.ReservationItems
                .Include(x => x.Reservation)
                .Where(x => x.AllotmentRoomTypeId != null &&
                            artIds.Contains(x.AllotmentRoomTypeId.Value) &&
                            x.Reservation!.Status != ReservationStatus.Cancelled &&
                            (x.StartDate ?? DateTime.MinValue) < rangeEndEx &&
                            (x.EndDate ?? DateTime.MaxValue) > rangeStart)
                .AsNoTracking()
                .ToListAsync();

            // per (artId, day) reserved qty
            var dayReserved = new Dictionary<(int artId, DateTime day), int>();
            var anyPaidArt = new HashSet<int>(
                items.Where(i => i.IsPaid && i.AllotmentRoomTypeId.HasValue)
                     .Select(i => i.AllotmentRoomTypeId!.Value));

            // cache lookup
            var artById = artList.ToDictionary(rt => rt.Id, rt => rt);
            var allotByArt = artList.ToDictionary(rt => rt.Id, rt => rt.Allotment!);

            foreach (var it in items)
            {
                var artId = it.AllotmentRoomTypeId!.Value;
                var allot = allotByArt[artId];

                var s = ((it.StartDate ?? allot.StartDate) < rangeStart ? rangeStart : (it.StartDate ?? allot.StartDate)).Date;
                var e = ((it.EndDate ?? allot.EndDate) > rangeEndEx ? rangeEndEx : (it.EndDate ?? allot.EndDate)).Date;

                for (var d = s; d < e; d = d.AddDays(1))
                {
                    var key = (artId, d);
                    dayReserved[key] = dayReserved.TryGetValue(key, out var cur) ? cur + it.Qty : it.Qty;
                }
            }

            // rows: ensure EXACTLY one cell per header day
            foreach (var a in allotments)
            {
                foreach (var rt in a.RoomTypes)
                {
                    var row = new PlanRowVM
                    {
                        Label = $"{a.Hotel!.Name} · {rt.RoomType!.Name} ({rt.Quantity}) · {a.StartDate:dd/MM}-{a.EndDate:dd/MM}"
                    };

                    for (var day = rangeStart; day < rangeEndEx; day = day.AddDays(1))
                    {
                        if (day < a.StartDate.Date || day >= a.EndDate.Date)
                        {
                            row.Cells.Add(new PlanCellVM { State = PlanCellState.Empty, Text = "" });
                            continue;
                        }

                        dayReserved.TryGetValue((rt.Id, day), out var reservedQty);
                        var free = Math.Max(0, rt.Quantity  - reservedQty);

                        PlanCellState state;
                        if (free == 0)
                        {
                            state = anyPaidArt.Contains(rt.Id) ? PlanCellState.FullPaid : PlanCellState.FullUnpaid;
                        }
                        else if (a.OptionDueDate.HasValue && a.OptionDueDate.Value.Date < DateTime.Today)
                        {
                            state = PlanCellState.Overdue;
                        }
                        else if (a.OptionDueDate.HasValue && a.OptionDueDate.Value.Date <= DateTime.Today.AddDays(3))
                        {
                            state = PlanCellState.FreeDueSoon;
                        }
                        else
                        {
                            state = PlanCellState.FreePaid;
                        }

                        row.Cells.Add(new PlanCellVM
                        {
                            State = state,
                            Text = free > 0 ? free.ToString() : "0",
                            Tooltip = $"{a.Title} | Free: {free}/{rt.Quantity}\nPrice: {rt.PricePerNight:0.##} €"
                        });
                    }

                    Rows.Add(row);
                }
            }
        }
    }

    public class PlanRowVM
    {
        public string Label { get; set; } = string.Empty;
        public ObservableCollection<PlanCellVM> Cells { get; } = new();
    }

    public class PlanCellVM
    {
        public PlanCellState State { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Tooltip { get; set; }
    }
}