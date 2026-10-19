using MAQ_API.Models;

namespace MAQ_API.Dtos;

public record NewsResponse(
    int NewsId,
    string Title,
    EventType Type,
    bool HasImage,
    string BlogContent,
    DateTime CreatedAt)
{
    public static NewsResponse From(News n) => new(
        n.NewsId, n.Title, n.Type, n.Image is not null, n.BlogContent, n.CreatedAt);
}
