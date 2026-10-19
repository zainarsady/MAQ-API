using MAQ_API.Models;

namespace MAQ_API.Dtos;

public record EventResponse(
    int EventId,
    string Title,
    DateOnly Date,
    bool HasImage,
    EventType EventType,
    TimeOnly Time,
    string Venue,
    string Description,
    int Capacity,
    DateTime CreatedAt)
{
    public static EventResponse From(Event e) => new(
        e.EventId, e.Title, e.Date, e.Image is not null, e.EventType, e.Time,
        e.Venue, e.Description, e.Capacity, e.CreatedAt);
}
