namespace PropertyViewingSlots.Api.Contracts;

public sealed record BookViewingRequest(
    string? PropertyId,
    string? UserId,
    DateTimeOffset StartTime);

public sealed record ViewingBookingResponse(
    Guid Id,
    string PropertyId,
    string UserId,
    DateTimeOffset StartTimeUtc);

public sealed record AvailableSlotResponse(DateTimeOffset StartTimeUtc);

public sealed record AvailableViewingSlotsResponse(
    string PropertyId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AvailableSlotResponse> Slots);
