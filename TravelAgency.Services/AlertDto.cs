using TravelAgency.Domain.Enums;

namespace TravelAgency.Services;

public record AlertDto(
    string Message,
    DateTime DueDate,
    Severity Severity,
    string? Link = null,
    string? HotelName = null,
    string? Country = null,
    string? CustomerName = null
);
