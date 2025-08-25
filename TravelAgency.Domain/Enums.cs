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
}