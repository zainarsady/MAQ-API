using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface IEventService
{
    Task<List<Event>> GetAllAsync();
    Task<Event?> GetByIdAsync(int id);
    Task<Event> CreateAsync(EventRequest request);
    Task<Event?> UpdateAsync(int id, EventRequest request);
    Task<bool> DeleteAsync(int id);
    Task<(byte[] Data, string ContentType)?> GetImageAsync(int id);
}
