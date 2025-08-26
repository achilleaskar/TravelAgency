namespace TravelAgency.Domain.Entities
{
    public class UpdateLog
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = ""; // "Hotel", "Customer", etc.
        public int EntityId { get; set; }
        public DateTime ChangedAt { get; set; }      // UTC
        public string? ChangedBy { get; set; }       // optional, set to current user if you add auth later
        public string Field { get; set; } = "";      // property name
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Note { get; set; }            // optional comment
    }
}
