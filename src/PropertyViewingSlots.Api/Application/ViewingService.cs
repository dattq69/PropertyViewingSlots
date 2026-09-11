using PropertyViewingSlots.Api.Contracts;
using PropertyViewingSlots.Api.Domain;

namespace PropertyViewingSlots.Api.Application;

public interface IViewingService
{
    BookViewingResult Book(BookViewingCommand command);

    SearchAvailableSlotsResult SearchAvailableSlots(SearchAvailableSlotsQuery query);
}

public interface IViewingRepository
{
    bool TryAdd(ViewingBooking booking);

    ViewingBooking? GetById(Guid bookingId);

    IReadOnlyCollection<ViewingBooking> GetByProperty(string propertyId);
}

public sealed record BookViewingCommand(string? PropertyId, string? UserId, DateTimeOffset StartTime);

public sealed record SearchAvailableSlotsQuery(string? PropertyId, DateOnly From, DateOnly To);

public enum BookingStatus
{
    Created,
    ValidationFailed,
    Conflict
}

public sealed record BookViewingResult(
    BookingStatus Status,
    ViewingBooking? Booking,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public static BookViewingResult Created(ViewingBooking booking) =>
        new(BookingStatus.Created, booking, EmptyErrors);

    public static BookViewingResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(BookingStatus.ValidationFailed, null, errors);

    public static BookViewingResult Conflict() =>
        new(BookingStatus.Conflict, null, EmptyErrors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>();
}

public sealed record SearchAvailableSlotsResult(
    string? PropertyId,
    IReadOnlyList<DateTimeOffset> Slots,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static SearchAvailableSlotsResult Success(string propertyId, IReadOnlyList<DateTimeOffset> slots) =>
        new(propertyId, slots, EmptyErrors);

    public static SearchAvailableSlotsResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, Array.Empty<DateTimeOffset>(), errors);

    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>();
}

public sealed class ViewingService(
    IViewingRepository repository,
    IPropertyLocationProvider propertyLocationProvider,
    TimeProvider timeProvider) : IViewingService
{
    public BookViewingResult Book(BookViewingCommand command)
    {
        var propertyId = command.PropertyId?.Trim();
        var userId = command.UserId?.Trim();
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(propertyId))
        {
            errors["propertyId"] = ["Property ID is required."];
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            errors["userId"] = ["User ID is required."];
        }

        ResolvedViewingLocation? location = null;
        if (!string.IsNullOrWhiteSpace(propertyId))
        {
            if (!propertyLocationProvider.TryGet(propertyId, out var resolvedLocation))
            {
                errors["propertyId"] = ["Property ID is not mapped to a viewing location."];
            }
            else
            {
                location = resolvedLocation;
            }
        }

        if (command.StartTime.Offset != TimeSpan.Zero)
        {
            errors["startTime"] = ["Start time must use a UTC offset of +00:00."];
        }

        if (location is not null)
        {
            var localStart = TimeZoneInfo.ConvertTime(command.StartTime, location.TimeZone);
            if (!IsSlotBoundary(localStart))
            {
                errors["startTime"] = ["Start time must be on a 30-minute boundary in the property's local time zone."];
            }
            else if (localStart.TimeOfDay < location.FirstSlotStart.ToTimeSpan() ||
                     localStart.TimeOfDay > location.LastSlotStart.ToTimeSpan())
            {
                errors["startTime"] =
                [
                    $"Start time must be between {location.FirstSlotStart:HH\\:mm} and {location.LastSlotStart:HH\\:mm} in {location.Name}."
                ];
            }
        }

        if (command.StartTime.ToUniversalTime() <= timeProvider.GetUtcNow())
        {
            errors["startTime"] = ["Start time must be in the future."];
        }

        if (errors.Count > 0)
        {
            return BookViewingResult.ValidationFailed(errors);
        }

        var booking = new ViewingBooking(
            Guid.NewGuid(),
            propertyId!,
            userId!,
            command.StartTime.ToUniversalTime());

        return repository.TryAdd(booking)
            ? BookViewingResult.Created(booking)
            : BookViewingResult.Conflict();
    }

    public SearchAvailableSlotsResult SearchAvailableSlots(SearchAvailableSlotsQuery query)
    {
        var propertyId = query.PropertyId?.Trim();
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(propertyId))
        {
            errors["propertyId"] = ["Property ID is required."];
        }

        if (query.From > query.To)
        {
            errors["dateRange"] = ["The from date must not be after the to date."];
        }

        ResolvedViewingLocation? location = null;
        if (!string.IsNullOrWhiteSpace(propertyId))
        {
            if (!propertyLocationProvider.TryGet(propertyId, out var resolvedLocation))
            {
                errors["propertyId"] = ["Property ID is not mapped to a viewing location."];
            }
            else
            {
                location = resolvedLocation;
            }
        }

        if (errors.Count > 0)
        {
            return SearchAvailableSlotsResult.ValidationFailed(errors);
        }

        var bookedSlotStarts = repository.GetByProperty(propertyId!)
            .Select(booking => booking.StartTimeUtc)
            .ToHashSet();
        var now = timeProvider.GetUtcNow();
        var slots = new List<DateTimeOffset>();

        for (var date = query.From; ; date = date.AddDays(1))
        {
            for (var start = location!.FirstSlotStart; start <= location.LastSlotStart; start = start.AddMinutes(30))
            {
                var localSlot = date.ToDateTime(start, DateTimeKind.Unspecified);
                if (location.TimeZone.IsInvalidTime(localSlot))
                {
                    continue;
                }

                var slotStartUtc = new DateTimeOffset(
                    TimeZoneInfo.ConvertTimeToUtc(localSlot, location.TimeZone),
                    TimeSpan.Zero);

                if (slotStartUtc <= now || bookedSlotStarts.Contains(slotStartUtc))
                {
                    continue;
                }

                slots.Add(slotStartUtc);
            }

            if (date == query.To)
            {
                break;
            }
        }

        return SearchAvailableSlotsResult.Success(propertyId!, slots);
    }

    private static bool IsSlotBoundary(DateTimeOffset localStart) =>
        (localStart.Minute is 0 or 30) && localStart.Second == 0 && localStart.Millisecond == 0;
}

public static class ViewingMapper
{
    public static ViewingBookingResponse ToResponse(ViewingBooking booking) =>
        new(
            booking.Id,
            booking.PropertyId,
            booking.UserId,
            booking.StartTimeUtc);

    public static AvailableSlotResponse ToResponse(DateTimeOffset startTime) => new(startTime);
}
