// Services/AllotmentService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Desktop.ViewModels;
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Services
{
    public class AllotmentService : IAllotmentService
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;

        public AllotmentService(IDbContextFactory<TravelAgencyDbContext> dbf)
            => _dbf = dbf;

        // -------- Lookups --------
        public async Task<(IEnumerable<CityVM> cities, IEnumerable<HotelVM> hotels, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var cities = await db.Cities
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CityVM { Id = c.Id, Name = c.Name })
                .ToListAsync();

            var hotels = await db.Hotels
                .AsNoTracking()
                .OrderBy(h => h.Name)
                .Select(h => new HotelVM { Id = h.Id, Name = h.Name, CityId = h.CityId })
                .ToListAsync();

            var roomTypes = await db.RoomTypes
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoomTypeVM { Id = r.Id, Name = r.Name })
                .ToListAsync();

            return (cities, hotels, roomTypes);
        }

        // -------- Load by Id (for edit) --------
        public async Task<AllotmentDto> LoadAsync(int id)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            var a = await db.Allotments
                .Include(x => x.Hotel)
                .Include(x => x.RoomTypes)
                .Include(x => x.Payments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == id);

            var dto = new AllotmentDto
            {
                Id = a.Id,
                Title = a.Title,
                CityId = a.Hotel?.CityId ?? 0,
                HotelId = a.HotelId,
                StartDateUtc = a.StartDate.ToUniversalTime(),
                EndDateUtc = a.EndDate.ToUniversalTime(),
                OptionDueUtc = a.OptionDueDate?.ToUniversalTime(),
                DatePolicy = a.DatePolicy == AllotmentDatePolicy.PartialAllowed ? "PartialAllowed" : "ExactDates",
                Lines = a.RoomTypes
                    .Select(l => new AllotmentLineDto
                    {
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = l.Currency,
                        Notes = l.Notes
                    })
                    .ToList(),
                Payments = a.Payments
                    .OrderBy(p => p.Date)
                    .Select(p => new PaymentDto
                    {
                        DateUtc = p.Date.ToUniversalTime(),
                        Title = p.Title,
                        Kind = p.Kind.ToString(),
                        Amount = p.Amount,
                        Currency = p.Currency,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided
                    })
                    .ToList(),
                History = new List<HistoryDto>() // γεμίζει παρακάτω αν υπάρχει πίνακας logs
            };

            // Optional: Load history (αν έχεις UpdateLogs table)
            if (db.UpdateLogs != null)
            {
                var lineIds = await db.AllotmentRoomTypes
                    .Where(rt => rt.AllotmentId == id)
                    .Select(rt => rt.Id)
                    .ToListAsync();

                var paymentIds = await db.AllotmentPayments
                    .Where(p => p.AllotmentId == id)
                    .Select(p => p.Id)
                    .ToListAsync();

                var logs = await db.UpdateLogs
                    .Where(u =>
                           (u.EntityName == nameof(Allotment) && u.EntityId == id) ||
                           (u.EntityName == nameof(AllotmentRoomType) && lineIds.Contains(u.EntityId)) ||
                           (u.EntityName == nameof(AllotmentPayment) && paymentIds.Contains(u.EntityId)))
                    .OrderByDescending(u => u.ChangedAt)
                    .Take(200)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var h in logs)
                {
                    dto.History.Add(new HistoryDto
                    {
                        ChangedAtUtc = h.ChangedAt,
                        ChangedBy = h.ChangedBy,
                        EntityType = h.EntityName,
                        Property = h.PropertyName,
                        OldValue = h.OldValue,
                        NewValue = h.NewValue
                    });
                }
            }

            return dto;
        }

        // -------- Save (new or edit) --------
        public async Task<SaveResult> SaveAsync(AllotmentDto dto)
        {
            await using var db = await _dbf.CreateDbContextAsync();

            // map string -> enum
            var policy = dto.DatePolicy == "PartialAllowed" ? AllotmentDatePolicy.PartialAllowed : AllotmentDatePolicy.ExactDates;

            // CityId έρχεται μόνο για filtering στα lookups — ο entity κρατά HotelId
            Allotment entity;

            if (dto.Id == null)
            {
                entity = new Allotment
                {
                    Title = dto.Title.Trim(),
                    HotelId = dto.HotelId,
                    StartDate = dto.StartDateUtc.ToLocalTime().Date,
                    EndDate = dto.EndDateUtc.ToLocalTime().Date,
                    OptionDueDate = dto.OptionDueUtc?.ToLocalTime().Date,
                    DatePolicy = policy,
                    Status = AllotmentStatus.Active
                };
                db.Allotments.Add(entity);
                await db.SaveChangesAsync();

                // Lines
                foreach (var l in dto.Lines)
                {
                    db.AllotmentRoomTypes.Add(new AllotmentRoomType
                    {
                        AllotmentId = entity.Id,
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = string.IsNullOrWhiteSpace(l.Currency) ? "EUR" : l.Currency!,
                        Notes = l.Notes
                    });
                }

                // Payments
                foreach (var p in dto.Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                    db.AllotmentPayments.Add(new AllotmentPayment
                    {
                        AllotmentId = entity.Id,
                        Date = p.DateUtc.ToLocalTime().Date,
                        Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                        Kind = kind,
                        Amount = p.Amount,
                        Currency = string.IsNullOrWhiteSpace(p.Currency) ? "EUR" : p.Currency!,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided
                    });
                }

                await db.SaveChangesAsync();

                return new SaveResult
                {
                    Success = true,
                    Id = entity.Id,
                    History = new List<HistoryDto>() // αν κρατάς logs, γέμισέ το εδώ
                };
            }
            else
            {
                entity = await db.Allotments
                    .Include(x => x.RoomTypes)
                    .Include(x => x.Payments)
                    .FirstAsync(x => x.Id == dto.Id.Value);

                entity.Title = dto.Title.Trim();
                entity.HotelId = dto.HotelId;
                entity.StartDate = dto.StartDateUtc.ToLocalTime().Date;
                entity.EndDate = dto.EndDateUtc.ToLocalTime().Date;
                entity.OptionDueDate = dto.OptionDueUtc?.ToLocalTime().Date;
                entity.DatePolicy = policy;

                // Replace Lines (MVP)
                db.AllotmentRoomTypes.RemoveRange(entity.RoomTypes);
                foreach (var l in dto.Lines)
                {
                    db.AllotmentRoomTypes.Add(new AllotmentRoomType
                    {
                        AllotmentId = entity.Id,
                        RoomTypeId = l.RoomTypeId,
                        Quantity = l.Quantity,
                        PricePerNight = l.PricePerNight,
                        Currency = string.IsNullOrWhiteSpace(l.Currency) ? "EUR" : l.Currency!,
                        Notes = l.Notes
                    });
                }

                // Replace Payments (MVP)
                db.AllotmentPayments.RemoveRange(entity.Payments);
                foreach (var p in dto.Payments)
                {
                    var kind = Enum.TryParse<PaymentKind>(p.Kind, true, out var k) ? k : PaymentKind.Deposit;
                    db.AllotmentPayments.Add(new AllotmentPayment
                    {
                        AllotmentId = entity.Id,
                        Date = p.DateUtc.ToLocalTime().Date,
                        Title = string.IsNullOrWhiteSpace(p.Title) ? "Payment" : p.Title.Trim(),
                        Kind = kind,
                        Amount = p.Amount,
                        Currency = string.IsNullOrWhiteSpace(p.Currency) ? "EUR" : p.Currency!,
                        Notes = p.Notes,
                        IsVoided = p.IsVoided
                    });
                }

                await db.SaveChangesAsync();

                return new SaveResult
                {
                    Success = true,
                    Id = entity.Id,
                    History = new List<HistoryDto>()
                };
            }
        }
    }
}
