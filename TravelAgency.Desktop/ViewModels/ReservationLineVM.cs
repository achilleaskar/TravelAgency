using System.ComponentModel;
using System.Runtime.CompilerServices;
using TravelAgency.Domain.Dtos;

public class ReservationLineVM : INotifyPropertyChanged
{
    public int? Id { get; set; }

    private RoomTypeVM? _roomType; private int _roomTypeId; private int _qty; private decimal _ppn; private string? _notes; private int _n; private decimal _lineTotal;

    public int RoomTypeId { get => _roomType?.Id ?? _roomTypeId; set { if (_roomTypeId == value) return; _roomTypeId = value; ResolveRoomTypeFromId?.Invoke(value); OnPropertyChanged(nameof(RoomTypeId)); } }
    internal Action<int>? ResolveRoomTypeFromId { get; set; }

    public RoomTypeVM? RoomType { get => _roomType; set { if (Set(ref _roomType, value)) { _roomTypeId = value?.Id ?? 0; OnPropertyChanged(nameof(RoomTypeId)); Recalc(); } } }
    public int Quantity { get => _qty; set { if (Set(ref _qty, value)) Recalc(); } }
    public decimal PricePerNight { get => _ppn; set { if (Set(ref _ppn, value)) Recalc(); } }
    public string? Notes { get => _notes; set => Set(ref _notes, value); }

    public decimal LineTotal { get => _lineTotal; private set => Set(ref _lineTotal, value); }
    public void SetNightsForLineTotal(int nights) { _n = nights; Recalc(); }
    private void Recalc() => LineTotal = _n * _ppn * _qty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null) { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; PropertyChanged?.Invoke(this, new(n)); return true; }
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}
