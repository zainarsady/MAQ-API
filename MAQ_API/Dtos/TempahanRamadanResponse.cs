using MAQ_API.Models;

namespace MAQ_API.Dtos;

/// <summary>
/// Admin view of a booking. Adds the aggregated payment status: Paid if any payment attempt for the
/// booking is Paid; otherwise Pending beats Failed; null when the booking has no payments yet.
/// </summary>
public record TempahanRamadanResponse(
    int TempahanRamadanId,
    DateOnly BookedDate,
    string CustomerName,
    string ContactNumber,
    string? ICNumber,
    int? Age,
    List<string>? ArwahName,
    string? Address,
    decimal Amount,
    DateTime CreatedAt,
    PaymentStatus? PaymentStatus)
{
    public static TempahanRamadanResponse From(TempahanRamadan booking, PaymentStatus? paymentStatus) => new(
        booking.TempahanRamadanId, booking.BookedDate, booking.CustomerName, booking.ContactNumber,
        booking.ICNumber, booking.Age, booking.ArwahName, booking.Address, booking.Amount,
        booking.CreatedAt, paymentStatus);
}
