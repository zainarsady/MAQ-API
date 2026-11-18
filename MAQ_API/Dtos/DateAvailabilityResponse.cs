namespace MAQ_API.Dtos;

public record DateAvailabilityResponse(DateOnly Date, int MaxBookings, int Booked, int Remaining);
