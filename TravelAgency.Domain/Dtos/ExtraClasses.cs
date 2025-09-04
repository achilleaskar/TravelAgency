using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TravelAgency.Domain.Dtos
{
    public class CityVM
    { public int Id { get; set; } public string Name { get; set; } = ""; }

    public class HotelVM
    { public int Id { get; set; } public string Name { get; set; } = ""; public int CityId { get; set; } }

    public class RoomTypeVM
    { public int Id { get; set; } public string Name { get; set; } = ""; }

    public class AllotmentDto
    {
        public int? Id { get; set; }
        public string Title { get; set; } = "";
        public int CityId { get; set; }
        public int HotelId { get; set; }
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }
        public DateTime? OptionDueUtc { get; set; }
        public string DatePolicy { get; set; } = "ExactDates";
        public List<AllotmentLineDto> Lines { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public List<HistoryDto> History { get; set; } = new();
    }

    public class AllotmentLineDto
    {
        public int RoomTypeId { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerNight { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? Notes { get; set; }
    }

    public class PaymentDto
    {
        public DateTime DateUtc { get; set; }
        public string Title { get; set; } = "";
        public string Kind { get; set; } = "Deposit";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? Notes { get; set; }
        public bool IsVoided { get; set; }
    }

    public class HistoryDto
    {
        public DateTime ChangedAtUtc { get; set; }
        public string? ChangedBy { get; set; }
        public string EntityType { get; set; } = "";
        public string Property { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }

    public class SaveResult
    {
        public bool Success { get; set; }
        public int Id { get; set; }
        public List<HistoryDto> History { get; set; } = new();
    }
}
