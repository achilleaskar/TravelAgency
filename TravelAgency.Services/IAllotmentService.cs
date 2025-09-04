// Desktop/ViewModels/IAllotmentService.cs
// using TravelAgency.Domain.Entities; // αν θες map

using TravelAgency.Domain.Dtos;

namespace TravelAgency.Desktop.ViewModels
{
    public interface IAllotmentService
    {
        Task<(IEnumerable<CityVM> cities, IEnumerable<HotelVM> hotels, IEnumerable<RoomTypeVM> roomTypes)> LoadLookupsAsync();

        Task<AllotmentDto> LoadAsync(int id);

        Task<SaveResult> SaveAsync(AllotmentDto dto);
    }
}