using CommunityToolkit.Mvvm.Messaging.Messages;

public sealed class BusyMessage : ValueChangedMessage<bool>
{
    public BusyMessage(bool value) : base(value) { }
}
