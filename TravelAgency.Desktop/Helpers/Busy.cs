using CommunityToolkit.Mvvm.Messaging;

namespace TravelAgency.Desktop.Helpers
{
public static class Busy
{
    private static int _count;

    private static void Publish(bool on) =>
        WeakReferenceMessenger.Default.Send(new BusyMessage(on));

    /// Begin/End (for manual scoping, nested-safe)
    public static IDisposable Begin()
    {
        if (Interlocked.Increment(ref _count) == 1)
            Publish(true);
        return new EndScope();
    }

    private sealed class EndScope : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            if (Interlocked.Decrement(ref _count) == 0)
                Publish(false);
        }
    }

    /// Run an async action under busy
    public static async Task RunAsync(Func<Task> action)
    {
        using (Begin())
            await action().ConfigureAwait(false);
    }

    /// Run an async func<T> under busy
    public static async Task<T> RunAsync<T>(Func<Task<T>> func)
    {
        using (Begin())
            return await func().ConfigureAwait(false);
    }
}
}