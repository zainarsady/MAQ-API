namespace MAQ_API.Exceptions;

public class DuplicateBookingException()
    : Exception("This IC number already has a booking on that date.");
