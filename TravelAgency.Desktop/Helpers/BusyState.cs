using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading;

namespace TravelAgency.Desktop.Helpers
{
public sealed partial class BusyState : ObservableObject, IRecipient<BusyMessage>
{
    public static BusyState Instance { get; } = new();

    private BusyState() => WeakReferenceMessenger.Default.Register(this);

    private int _count;

    [ObservableProperty]
    private bool isBusy;

    public void Receive(BusyMessage message)
    {
        if (message.Value) // was IsBusy
        {
            if (Interlocked.Increment(ref _count) == 1)
                IsBusy = true;
        }
        else
        {
            var n = Interlocked.Decrement(ref _count);
            if (n <= 0)
            {
                _count = 0; // guard
                IsBusy = false;
            }
        }
    }
}
}