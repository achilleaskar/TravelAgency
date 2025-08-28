namespace TravelAgency.Services
{
    public interface IUiDispatcher
    {
        // Execute on UI thread (sync). If no UI context, just run the action inline.
        void Invoke(Action action);

        // True if we are currently on the UI thread (if one exists)
        bool CheckAccess();
    }
}
