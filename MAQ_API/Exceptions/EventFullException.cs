namespace MAQ_API.Exceptions;

public class EventFullException(int remaining)
    : Exception($"Not enough capacity left for this event ({remaining} spot(s) remaining).");
