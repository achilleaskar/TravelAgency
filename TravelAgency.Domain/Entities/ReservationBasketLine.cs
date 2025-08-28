namespace TravelAgency.Domain.Models
{
    /// <summary>
    /// One line the user is composing before saving the Reservation.
    /// Kind = "AllotmentRoom" or "Service".
    /// </summary>
    public class ReservationBasketLine
    {
        public string Kind { get; set; } = "Service";

        // For services
        public string Title { get; set; } = "";

        // For allotment rooms
        public int? AllotmentRoomTypeId { get; set; }

        // Common numeric fields
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public string Currency { get; set; } = "EUR";
    }
}
