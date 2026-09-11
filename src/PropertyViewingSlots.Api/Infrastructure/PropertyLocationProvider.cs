using Microsoft.Extensions.Options;
using PropertyViewingSlots.Api.Application;
using PropertyViewingSlots.Api.Configuration;

namespace PropertyViewingSlots.Api.Infrastructure;

public sealed class PropertyLocationProvider : IPropertyLocationProvider
{
    private readonly IReadOnlyDictionary<string, ResolvedViewingLocation> _locations;
    private readonly IReadOnlyDictionary<string, string> _propertyLocations;

    public PropertyLocationProvider(IOptions<ViewingSlotsOptions> options)
    {
        var configuredOptions = options.Value;
        _locations = configuredOptions.Locations.ToDictionary(
            pair => pair.Key,
            pair => new ResolvedViewingLocation(
                pair.Key,
                TimeZoneInfo.FindSystemTimeZoneById(pair.Value.TimeZoneId!),
                ParseSlotStart(pair.Value.FirstSlotStart!),
                ParseSlotStart(pair.Value.LastSlotStart!)),
            StringComparer.Ordinal);
        _propertyLocations = new Dictionary<string, string>(configuredOptions.PropertyLocations, StringComparer.Ordinal);
    }

    public bool TryGet(string propertyId, out ResolvedViewingLocation location)
    {
        if (_propertyLocations.TryGetValue(propertyId, out var locationName) &&
            _locations.TryGetValue(locationName, out var resolvedLocation))
        {
            location = resolvedLocation!;
            return true;
        }

        location = default!;
        return false;
    }

    private static TimeOnly ParseSlotStart(string value)
    {
        return ViewingSlotsOptionParser.TryParseSlotStart(value, out var slotStart)
            ? slotStart
            : throw new InvalidOperationException("Viewing slot options were not validated before use.");
    }
}
