namespace Hub.Infrastructure.Events;

public interface IEventMirror
{
    Task Mirror(RunEvent item);
}
