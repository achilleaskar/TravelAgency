namespace TravelAgency.Domain.Dtos
{
    // Lightweight DTOs (service-side)
    public class CityVM { public int Id { get; set; } public string Name { get; set; } = ""; }
    public class HotelVM { public int Id { get; set; } public string Name { get; set; } = ""; public int CityId { get; set; } }
    public class RoomTypeVM { public int Id { get; set; } public string Name { get; set; } = ""; }
    public class CustomerVM { public int Id { get; set; } public string Name { get; set; } = ""; }

    public class AllotmentDto
    {
        public int? Id { get; set; }
        public string Title { get; set; } = "";
        public int CityId { get; set; }           // used for UI filter; entity stores HotelId
        public int HotelId { get; set; }
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }
        public DateTime? OptionDueUtc { get; set; }

        // IMPORTANT: repo uses AllotmentDatePolicy (not DatePolicy)
        // Keep as string for simple XAML binding: "ExactDates" | "PartialAllowed"
        public string AllotmentDatePolicy { get; set; } = "ExactDates";

        public List<AllotmentLineDto> Lines { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public List<HistoryDto> History { get; set; } = new();
    }

    public class AllotmentLineDto
    {
        public int? Id { get; set; }            // <-- line id (AllotmentRoomType.Id)
        public int RoomTypeId { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerNight { get; set; }
        public string? Notes { get; set; }
    }


    // TravelAgency.Domain/Dtos/PaymentDto.cs
    public sealed class PaymentDto
    {
        public int? Id { get; set; }                 // already (if you use upsert-by-id)
        public DateTime DateUtc { get; set; }
        public string Title { get; set; } = "";
        public string Kind { get; set; } = "Deposit";
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        public bool IsVoided { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }  // <-- NEW
    }


    public class HistoryDto
    {
        public DateTime ChangedAtUtc { get; set; }
        public string? ChangedBy { get; set; }

        // IMPORTANT: repo uses EntityName (not EntityType)
        public string EntityName { get; set; } = "";

        public string PropertyName { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public sealed class SaveResult
    {
        public bool Success { get; set; }
        public int? Id { get; set; }
        public string? Message { get; set; }

        // Add this so editors can refresh history without a second fetch
        public List<HistoryDto>? History { get; set; }
    }
}
