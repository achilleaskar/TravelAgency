using System.ComponentModel.DataAnnotations;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class Reservation : AuditableEntity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty; // π.χ. Καππαδοκία 20-25 Αυγούστου

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public DateTime? DepositDueDate { get; set; }
    public DateTime? BalanceDueDate { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Draft;
    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<ReservationItem> Items { get; set; } = new List<ReservationItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
