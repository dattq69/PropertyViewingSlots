namespace PropertyViewingSlots.Api.Domain;

public sealed record ViewingBooking(
    Guid Id,
    string PropertyId,
    string UserId,
    DateTimeOffset StartTimeUtc);
