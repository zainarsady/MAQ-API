namespace MAQ_API.Dtos;

/// <summary>
/// All three counts are in SLOTS (one slot = RM300 of a booking's Amount), not booking rows:
/// a 3-slot booking adds 3 to <paramref name="Booked"/>. Only bookings with a Paid payment are counted.
/// </summary>
public record DateAvailabilityResponse(DateOnly Date, int MaxBookings, int Booked, int Remaining);
