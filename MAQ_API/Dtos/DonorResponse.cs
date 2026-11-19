namespace MAQ_API.Dtos;

/// <summary>Public, minimal view of a paid booking. Deliberately carries no contact details.</summary>
public record DonorResponse(string Id, string DisplayName);
