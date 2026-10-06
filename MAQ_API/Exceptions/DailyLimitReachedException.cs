namespace MAQ_API.Exceptions;

public class DailyLimitReachedException(int limit)
    : Exception($"The maximum of {limit} bookings for that date has been reached.");
