namespace PropertyViewingSlots.Api.Application;

public sealed record ResolvedViewingLocation(
    string Name,
    TimeZoneInfo TimeZone,
    TimeOnly FirstSlotStart,
    TimeOnly LastSlotStart);

public interface IPropertyLocationProvider
{
    bool TryGet(string propertyId, out ResolvedViewingLocation location);
}
