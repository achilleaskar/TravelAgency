namespace TravelAgency.Domain.Entities
{
    public class UpdateLog
    {
        public long Id { get; set; }
        public string EntityName { get; set; } = ""; // "Allotment" / "AllotmentRoomType"
        public int EntityId { get; set; }
        public string PropertyName { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? ChangedBy { get; set; } // optional (αν αργότερα βάλεις users)

        // ευκολία: να μπορείς να δεις ιστορικό ανά allotment
        public int? AllotmentId { get; set; }
    }
}
