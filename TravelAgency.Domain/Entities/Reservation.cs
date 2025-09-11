using System.ComponentModel.DataAnnotations;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class Reservation : AuditableEntity
{
    public int Id { get; set; }

    public DateTime? DepositDueDate { get; set; }
    public DateTime? BalanceDueDate { get; set; }


    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Local dates (date-only semantics)
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }

    public ReservationStatus Status { get; set; } 

    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<ReservationLine> Lines { get; set; } = new List<ReservationLine>();
    public ICollection<ReservationPayment> Payments { get; set; } = new List<ReservationPayment>();
}
