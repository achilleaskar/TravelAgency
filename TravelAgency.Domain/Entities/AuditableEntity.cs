using TravelAgency.Domain.Entities;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }   // set on insert (UTC)
    public DateTime UpdatedAt { get; set; }   // set on update (UTC)
    public string? Notes { get; set; }        // free-form notes
    public ICollection<UpdateLog> UpdateLogs { get; set; } = new List<UpdateLog>();
}