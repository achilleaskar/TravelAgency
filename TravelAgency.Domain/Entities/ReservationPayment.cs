using System.ComponentModel.DataAnnotations.Schema;
using TravelAgency.Domain.Enums;

namespace TravelAgency.Domain.Entities;

public class ReservationPayment
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    // Local date in DB
    public DateTime Date { get; set; }

    public string Title { get; set; } = "Payment";
    public PaymentKind Kind { get; set; } = PaymentKind.Deposit;

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    public string? Notes { get; set; }
    public bool IsVoided { get; set; }

    public DateTime UpdatedAtUtc { get; set; }  // DB default recommended
}
