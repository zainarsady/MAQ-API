using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface ITempahanRamadanService
{
    Task<List<TempahanRamadan>> GetAllAsync();
    Task<TempahanRamadan?> GetByIdAsync(int id);
    Task<TempahanRamadan> CreateAsync(TempahanRamadanRequest request);
    Task<TempahanRamadan?> UpdateAsync(int id, TempahanRamadanRequest request);
    Task<bool> DeleteAsync(int id);
}
