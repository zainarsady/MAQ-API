using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface ITempahanRamadanService
{
    Task<List<TempahanRamadanResponse>> GetAllAsync();
    Task<TempahanRamadanResponse?> GetByIdAsync(int id);
    Task<List<DonorResponse>> GetPaidDonorsAsync(DateOnly date);
    Task<TempahanRamadan> CreateAsync(TempahanRamadanRequest request);
    Task<TempahanRamadan?> UpdateAsync(int id, TempahanRamadanRequest request);
    Task<bool> DeleteAsync(int id);
    Task<List<DateAvailabilityResponse>> GetAvailabilityAsync(DateOnly? from, DateOnly? to);
}
