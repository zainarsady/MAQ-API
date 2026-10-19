using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class EventService(AppDbContext db) : IEventService
{
    public Task<List<Event>> GetAllAsync() =>
        db.Events.AsNoTracking().OrderBy(e => e.Date).ThenBy(e => e.Time).ToListAsync();

    public Task<Event?> GetByIdAsync(int id) =>
        db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);

    public async Task<Event> CreateAsync(EventRequest request)
    {
        var image = await ImageUpload.ReadAsync(request.Image);

        var ev = new Event { CreatedAt = DateTime.UtcNow };
        Apply(ev, request, image);
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    public async Task<Event?> UpdateAsync(int id, EventRequest request)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return null;

        var image = await ImageUpload.ReadAsync(request.Image);

        Apply(ev, request, image);
        await db.SaveChangesAsync();
        return ev;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null) return false;

        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(int id)
    {
        var ev = await db.Events.AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => new { e.Image, e.ImageContentType })
            .FirstOrDefaultAsync();

        return ev?.Image is null ? null : (ev.Image, ev.ImageContentType ?? "application/octet-stream");
    }

    private static void Apply(Event ev, EventRequest request, (byte[] Data, string ContentType)? image)
    {
        ev.Title = request.Title;
        ev.Date = request.Date!.Value;
        ev.EventType = request.EventType!.Value;
        ev.Time = request.Time!.Value;
        ev.Venue = request.Venue;
        ev.Description = request.Description;
        ev.Capacity = request.Capacity;

        if (image is { } img)
        {
            ev.Image = img.Data;
            ev.ImageContentType = img.ContentType;
        }
    }
}
