namespace TravelAgency.Domain.Dtos
{
    public sealed class ReservationDto
    {
        public int? Id { get; set; }
        public int CustomerId { get; set; }
        public DateTime CheckInUtc { get; set; }
        public DateTime CheckOutUtc { get; set; }

        public List<ReservationLineDto> Lines { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public List<HistoryDto> History { get; set; } = new();
    }

    public sealed class ReservationLineDto
    {
        public int? Id { get; set; }

        // <-- IMPORTANT: consume concrete allotment line
        public int AllotmentRoomTypeId { get; set; }

        public int Quantity { get; set; }
        public decimal PricePerNight { get; set; }
        public string? Notes { get; set; }
    }
}
