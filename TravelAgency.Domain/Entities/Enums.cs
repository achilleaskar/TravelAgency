namespace TravelAgency.Domain.Enums
{
    public enum AllotmentStatus
    {
        Active = 0,
        PartiallyReleased = 1,
        Released = 2,
        Cancelled = 3
    }

    public enum ReservationStatus
    {
        Draft = 0,
        PendingDeposit = 1,
        Confirmed = 2,
        Cancelled = 3,
        Completed = 4
    }

    public enum ReservationItemKind
    {
        AllotmentRoom = 0,
        Service = 1
    }

    public enum Severity
    {
        Info = 0,
        Warning = 1,
        Danger = 2
    }

    public enum AllotmentDatePolicy
    {
        ExactDates = 0,     // οι ημερομηνίες είναι “κλειδωμένες”
        PartialAllowed = 1  // επιτρέπεται χρήση υποσυνόλου ημερών του range
    }

    public enum PaymentKind
    {
        Deposit = 0,
        Balance = 1,
        Other = 2
    }
}