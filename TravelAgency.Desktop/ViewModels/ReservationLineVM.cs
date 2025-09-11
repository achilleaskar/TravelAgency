using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ReservationLineVM : INotifyPropertyChanged
{
    public int? Id { get; set; }

    public int AllotmentRoomTypeId { get => _artId; set { if (Set(ref _artId, value)) Recalc(); } }
    public string Display { get => _display; set => Set(ref _display, value); }

    public int Quantity { get => _quantity; set { if (Set(ref _quantity, value)) Recalc(); } }
    public decimal PricePerNight { get => _price; set { if (Set(ref _price, value)) Recalc(); } }
    public string? Notes { get => _notes; set => Set(ref _notes, value); }

    public decimal LineTotal { get => _lineTotal; private set => Set(ref _lineTotal, value); }

    // Read-only public exposure if you want to show it in the UI
    public int Nights => _nights;

    private int _artId;
    private string _display = "";
    private int _quantity = 1;
    private decimal _price;
    private string? _notes;
    private decimal _lineTotal;
    private int _nights; // <- holds nights injected from parent (check-in/out)

    private void Recalc() => LineTotal = Quantity * PricePerNight * _nights;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    internal void SetNightsForLineTotal(int nights)
    {
        if (nights < 0) nights = 0;
        if (_nights != nights)
        {
            _nights = nights;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nights)));
            Recalc();
        }
    }
}
