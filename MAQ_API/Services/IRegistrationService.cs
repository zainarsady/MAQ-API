using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface IRegistrationService
{
    Task<List<Registration>?> GetAllAsync(int eventId);
    Task<Registration?> GetByIdAsync(int eventId, int id);
    Task<Registration?> CreateAsync(int eventId, RegistrationRequest request);
    Task<Registration?> UpdateAsync(int eventId, int id, RegistrationRequest request);
    Task<bool> DeleteAsync(int eventId, int id);
}
