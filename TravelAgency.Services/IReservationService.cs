// Services/IReservationService.cs
using TravelAgency.Domain.Dtos;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Services;

public interface IReservationService
{
    Task<(IEnumerable<CustomerVM> customers, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync();
    Task<ReservationDto> LoadAsync(int id);
    Task<SaveResult> SaveAsync(ReservationDto dto);
}
