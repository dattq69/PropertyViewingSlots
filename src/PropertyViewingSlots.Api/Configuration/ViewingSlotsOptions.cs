using System.Globalization;
using Microsoft.Extensions.Options;

namespace PropertyViewingSlots.Api.Configuration;

public sealed class ViewingSlotsOptions
{
    public const string SectionName = "ViewingSlots";

    public Dictionary<string, ViewingLocationOptions> Locations { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> PropertyLocations { get; init; } = new(StringComparer.Ordinal);
}

public sealed class ViewingLocationOptions
{
    public string? TimeZoneId { get; init; }

    public string? FirstSlotStart { get; init; }

    public string? LastSlotStart { get; init; }
}

public sealed class ViewingSlotsOptionsValidator : IValidateOptions<ViewingSlotsOptions>
{
    public ValidateOptionsResult Validate(string? name, ViewingSlotsOptions options)
    {
        var failures = new List<string>();

        if (options.Locations.Count == 0)
        {
            failures.Add("ViewingSlots:Locations must define at least one location.");
        }

        foreach (var (locationName, location) in options.Locations)
        {
            var prefix = $"ViewingSlots:Locations:{locationName}";
            var firstSlotIsValid = ViewingSlotsOptionParser.TryParseSlotStart(location.FirstSlotStart, out var firstSlotStart);
            var lastSlotIsValid = ViewingSlotsOptionParser.TryParseSlotStart(location.LastSlotStart, out var lastSlotStart);

            if (string.IsNullOrWhiteSpace(location.TimeZoneId))
            {
                failures.Add($"{prefix}:TimeZoneId is required.");
            }
            else
            {
                try
                {
                    _ = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    failures.Add($"{prefix}:TimeZoneId '{location.TimeZoneId}' cannot be resolved.");
                }
                catch (InvalidTimeZoneException)
                {
                    failures.Add($"{prefix}:TimeZoneId '{location.TimeZoneId}' is invalid.");
                }
            }

            if (!firstSlotIsValid)
            {
                failures.Add($"{prefix}:FirstSlotStart must use HH:mm format on a 30-minute boundary.");
            }

            if (!lastSlotIsValid)
            {
                failures.Add($"{prefix}:LastSlotStart must use HH:mm format on a 30-minute boundary.");
            }

            if (firstSlotIsValid && lastSlotIsValid && firstSlotStart > lastSlotStart)
            {
                failures.Add($"{prefix}:FirstSlotStart must not be after LastSlotStart.");
            }
        }

        foreach (var (propertyId, locationName) in options.PropertyLocations)
        {
            if (string.IsNullOrWhiteSpace(propertyId))
            {
                failures.Add("ViewingSlots:PropertyLocations cannot contain an empty property ID.");
            }

            if (string.IsNullOrWhiteSpace(locationName) || !options.Locations.ContainsKey(locationName))
            {
                failures.Add($"ViewingSlots:PropertyLocations:{propertyId} must reference a configured location.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

public static class ViewingSlotsOptionParser
{
    public static bool TryParseSlotStart(string? value, out TimeOnly slotStart)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out slotStart))
        {
            return false;
        }

        return slotStart.Minute is 0 or 30 && slotStart.Second == 0 && slotStart.Millisecond == 0;
    }
}
