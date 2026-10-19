using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class NewsService(AppDbContext db) : INewsService
{
    public Task<List<News>> GetAllAsync() =>
        db.News.AsNoTracking().OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NewsId).ToListAsync();

    public Task<News?> GetByIdAsync(int id) =>
        db.News.AsNoTracking().FirstOrDefaultAsync(n => n.NewsId == id);

    public async Task<News> CreateAsync(NewsRequest request)
    {
        var image = await ImageUpload.ReadAsync(request.Image);

        var news = new News { CreatedAt = DateTime.UtcNow };
        Apply(news, request, image);
        db.News.Add(news);
        await db.SaveChangesAsync();
        return news;
    }

    public async Task<News?> UpdateAsync(int id, NewsRequest request)
    {
        var news = await db.News.FindAsync(id);
        if (news is null) return null;

        var image = await ImageUpload.ReadAsync(request.Image);

        Apply(news, request, image);
        await db.SaveChangesAsync();
        return news;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var news = await db.News.FindAsync(id);
        if (news is null) return false;

        db.News.Remove(news);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(int id)
    {
        var news = await db.News.AsNoTracking()
            .Where(n => n.NewsId == id)
            .Select(n => new { n.Image, n.ImageContentType })
            .FirstOrDefaultAsync();

        return news?.Image is null ? null : (news.Image, news.ImageContentType ?? "application/octet-stream");
    }

    private static void Apply(News news, NewsRequest request, (byte[] Data, string ContentType)? image)
    {
        news.Title = request.Title;
        news.Type = request.Type!.Value;
        news.BlogContent = request.BlogContent;

        if (image is { } img)
        {
            news.Image = img.Data;
            news.ImageContentType = img.ContentType;
        }
    }
}
