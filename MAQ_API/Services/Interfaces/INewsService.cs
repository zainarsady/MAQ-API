using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface INewsService
{
    Task<List<News>> GetAllAsync();
    Task<News?> GetByIdAsync(int id);
    Task<News> CreateAsync(NewsRequest request);
    Task<News?> UpdateAsync(int id, NewsRequest request);
    Task<bool> DeleteAsync(int id);
    Task<(byte[] Data, string ContentType)?> GetImageAsync(int id);
}
