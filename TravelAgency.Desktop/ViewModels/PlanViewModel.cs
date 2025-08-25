using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TravelAgency.Data;
using TravelAgency.Desktop.Converters;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Desktop.ViewModels
{
    // Προσοχή: χρειάζεται το package CommunityToolkit.Mvvm στο Desktop project
    public partial class PlanViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;

        public ObservableCollection<City> Cities { get; } = new();
        public ObservableCollection<string> DayHeaders { get; } = new();
        public ObservableCollection<PlanRowVM> Rows { get; } = new();

        [ObservableProperty] private City? selectedCity;
        [ObservableProperty] private DateTime fromDate = DateTime.Today;
        [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(14);
        [ObservableProperty] private string? searchText;

        public PlanViewModel(TravelAgencyDbContext db)
        {
            _db = db;
            _ = LoadCitiesAsync();
            BuildHeaders();
        }

        // Τα παρακάτω partial καλούνται αυτόματα από το source generator όταν αλλάξουν οι ιδιότητες
        partial void OnFromDateChanged(DateTime value) => BuildHeaders();
        partial void OnToDateChanged(DateTime value) => BuildHeaders();

        private async Task LoadCitiesAsync()
        {
            Cities.Clear();
            var list = await _db.Cities.OrderBy(x => x.Name).ToListAsync();
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
            Rows.Clear();

            var q = _db.Allotments
                .Include(a => a.Hotel)!.ThenInclude(h => h.City)
                .Include(a => a.RoomTypes)!.ThenInclude(rt => rt.RoomType)
                .Where(a => a.StartDate <= ToDate && a.EndDate > FromDate); // overlap

            if (SelectedCity != null)
                q = q.Where(a => a.Hotel!.CityId == SelectedCity.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(a => a.Hotel!.Name.Contains(SearchText) || a.Title.Contains(SearchText));

            var allotments = await q.AsNoTracking().ToListAsync();

            // Συγκεντρώνουμε όλα τα ART ids
            var artList = allotments.SelectMany(a => a.RoomTypes).ToList();
            var artIds = artList.Select(rt => rt.Id).ToList();

            if (artIds.Count == 0) return;

            var rangeStart = FromDate.Date;
            var rangeEnd = ToDate.Date.AddDays(1); // exclusive

            // Prefetch: όλα τα ReservationItems για τα ARTs που τέμνουν το range
            var items = await _db.ReservationItems
                .Include(x => x.Reservation)
                .Where(x =>
                       x.AllotmentRoomTypeId != null &&
                       artIds.Contains(x.AllotmentRoomTypeId.Value) &&
                       x.Reservation!.Status != ReservationStatus.Cancelled &&
                       (x.StartDate ?? DateTime.MinValue) < rangeEnd &&
                       (x.EndDate ?? DateTime.MaxValue) > rangeStart)
                .AsNoTracking()
                .ToListAsync();

            // Για γρήγορη πρόσβαση στα allotments/rt ανά id
            var artMap = artList.ToDictionary(rt => rt.Id, rt => rt);
            var allotByArt = artList.ToDictionary(rt => rt.Id, rt => rt.Allotment!);

            // Υπολογισμός δεσμεύσεων ανά (artId, day)
            var dayReserved = new Dictionary<(int artId, DateTime day), int>();
            var anyPaidArt = new HashSet<int>(
                items.Where(i => i.IsPaid && i.AllotmentRoomTypeId.HasValue)
                     .Select(i => i.AllotmentRoomTypeId!.Value));

            foreach (var it in items)
            {
                var artId = it.AllotmentRoomTypeId!.Value;
                var allot = allotByArt[artId];

                // εύρος που πραγματικά μετράει για timeline
                var start = (it.StartDate ?? allot.StartDate).Date;
                var endExclusive = (it.EndDate ?? allot.EndDate).Date; // exclusive εδώ

                // intersect με τα φίλτρα
                var s = start < rangeStart ? rangeStart : start;
                var e = endExclusive > rangeEnd ? rangeEnd : endExclusive;

                for (var d = s; d < e; d = d.AddDays(1))
                {
                    var key = (artId, d);
                    dayReserved[key] = dayReserved.TryGetValue(key, out var cur) ? cur + it.Qty : it.Qty;
                }
            }

            // Χτίζουμε σειρές
            for (int aIndex = 0; aIndex < allotments.Count; aIndex++)
            {
                var a = allotments[aIndex];

                foreach (var rt in a.RoomTypes)
                {
                    var row = new PlanRowVM
                    {
                        Label = $"{a.Hotel!.Name} · {rt.RoomType!.Name} ({rt.Quantity}) · {a.StartDate:dd/MM}-{a.EndDate:dd/MM}"
                    };

                    for (var day = rangeStart; day < rangeEnd; day = day.AddDays(1))
                    {
                        // εκτός allotment -> Empty
                        if (day < a.StartDate.Date || day >= a.EndDate.Date)
                        {
                            row.Cells.Add(new PlanCellVM { State = PlanCellState.Empty, Text = "" });
                            continue;
                        }

                        dayReserved.TryGetValue((rt.Id, day), out var reservedQty);
                        var free = Math.Max(0, rt.Quantity - reservedQty);

                        PlanCellState state;
                        if (free == 0)
                        {
                            // πλήρες: πληρωμένα ή όχι;
                            state = anyPaidArt.Contains(rt.Id) ? PlanCellState.FullPaid : PlanCellState.FullUnpaid;
                        }
                        else if (a.OptionDueDate.HasValue && a.OptionDueDate.Value.Date < DateTime.Today)
                        {
                            state = PlanCellState.Overdue; // έχει λήξει
                        }
                        else if (a.OptionDueDate.HasValue && a.OptionDueDate.Value.Date <= DateTime.Today.AddDays(3))
                        {
                            state = PlanCellState.FreeDueSoon; // 3/2/1 μέρες
                        }
                        else
                        {
                            state = PlanCellState.FreePaid; // οκ
                        }

                        row.Cells.Add(new PlanCellVM
                        {
                            State = state,
                            Text = free > 0 ? free.ToString() : "0",
                            Tooltip = $"{a.Title} | Free: {free}/{rt.Quantity}\nPrice: {rt.PricePerNight:0.##} {rt.Currency}"
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
