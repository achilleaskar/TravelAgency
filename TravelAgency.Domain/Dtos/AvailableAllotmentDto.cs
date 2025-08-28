using System;

namespace TravelAgency.Domain.Models
{
    public class AvailableAllotmentDto
    {
        public int AllotmentId { get; set; }
        public string Title { get; set; } = "";
        public string HotelName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int RemainingTotal { get; set; }   // NEW
    }
}
