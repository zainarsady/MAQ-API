using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class RegistrationService(AppDbContext db) : IRegistrationService
{
    // Returns null when the event does not exist.
    public async Task<List<Registration>?> GetAllAsync(int eventId)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId)) return null;

        return await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.RegistrationId)
            .ToListAsync();
    }

    public Task<Registration?> GetByIdAsync(int eventId, int id) =>
        db.Registrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.RegistrationId == id);

    // Returns null when the event does not exist.
    public async Task<Registration?> CreateAsync(int eventId, RegistrationRequest request)
    {
        if (!await EnsureCapacityAsync(eventId, request.NumberOfAttendees, 0)) return null;

        var registration = new Registration { EventId = eventId, CreatedAt = DateTime.UtcNow };
        Apply(registration, request);
        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    public async Task<Registration?> UpdateAsync(int eventId, int id, RegistrationRequest request)
    {
        var registration = await db.Registrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.RegistrationId == id);
        if (registration is null) return null;

        await EnsureCapacityAsync(eventId, request.NumberOfAttendees, id);

        Apply(registration, request);
        await db.SaveChangesAsync();
        return registration;
    }

    public async Task<bool> DeleteAsync(int eventId, int id)
    {
        var registration = await db.Registrations
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.RegistrationId == id);
        if (registration is null) return false;

        db.Registrations.Remove(registration);
        await db.SaveChangesAsync();
        return true;
    }

    // False when the event does not exist; throws EventFullException when over capacity.
    private async Task<bool> EnsureCapacityAsync(int eventId, int requested, int excludeId)
    {
        var capacity = await db.Events.Where(e => e.EventId == eventId)
            .Select(e => (int?)e.Capacity).FirstOrDefaultAsync();
        if (capacity is null) return false;

        var taken = await db.Registrations
            .Where(r => r.EventId == eventId && r.RegistrationId != excludeId)
            .SumAsync(r => (int?)r.NumberOfAttendees) ?? 0;

        if (taken + requested > capacity) throw new EventFullException(Math.Max(0, capacity.Value - taken));
        return true;
    }

    private static void Apply(Registration registration, RegistrationRequest request)
    {
        registration.FullName = request.FullName;
        registration.Email = request.Email;
        registration.PhoneNumber = request.PhoneNumber;
        registration.NumberOfAttendees = request.NumberOfAttendees;
        registration.Notes = request.Notes;
    }
}
