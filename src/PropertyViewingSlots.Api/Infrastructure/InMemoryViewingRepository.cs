using PropertyViewingSlots.Api.Application;
using PropertyViewingSlots.Api.Domain;

namespace PropertyViewingSlots.Api.Infrastructure;

public sealed class InMemoryViewingRepository : IViewingRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<ViewingSlotKey, ViewingBooking> _bookingsBySlot = new();
    private readonly Dictionary<Guid, ViewingBooking> _bookingsById = new();

    public bool TryAdd(ViewingBooking booking)
    {
        var key = new ViewingSlotKey(booking.PropertyId, booking.StartTimeUtc);

        lock (_gate)
        {
            if (_bookingsBySlot.ContainsKey(key))
            {
                return false;
            }

            _bookingsBySlot.Add(key, booking);
            _bookingsById.Add(booking.Id, booking);
            return true;
        }
    }

    public ViewingBooking? GetById(Guid bookingId)
    {
        lock (_gate)
        {
            return _bookingsById.GetValueOrDefault(bookingId);
        }
    }

    public IReadOnlyCollection<ViewingBooking> GetByProperty(string propertyId)
    {
        lock (_gate)
        {
            return _bookingsBySlot.Values
                .Where(booking => string.Equals(booking.PropertyId, propertyId, StringComparison.Ordinal))
                .ToArray();
        }
    }

    private readonly record struct ViewingSlotKey(string PropertyId, DateTimeOffset StartTimeUtc);
}
