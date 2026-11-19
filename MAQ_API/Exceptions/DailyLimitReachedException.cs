namespace MAQ_API.Exceptions;

public class DailyLimitReachedException(int limit, int remaining)
    : Exception($"Only {remaining} of {limit} slots are left for that date.");
