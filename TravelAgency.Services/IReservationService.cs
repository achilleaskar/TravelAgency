// Services/IReservationService.cs
using TravelAgency.Domain.Dtos;

namespace TravelAgency.Services;

public interface IReservationService
{
    Task<(IEnumerable<CustomerVM> customers,
           IEnumerable<AllotmentOptionVM> allotmentOptions)> LoadLookupsAsync(DateTime? checkInUtc = null, DateTime? checkOutUtc = null);

    Task<ReservationDto> LoadAsync(int id);
    Task<SaveResult> SaveAsync(ReservationDto dto);
}
