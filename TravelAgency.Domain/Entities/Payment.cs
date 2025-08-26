using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TravelAgency.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    public DateTime PaymentDate { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string Method { get; set; } = "Cash"; // Card/Bank/etc.

    [MaxLength(300)]
    public string? Notes { get; set; }
}