using Microsoft.Extensions.Options;
using NUnit.Framework;
using PropertyViewingSlots.Api.Application;
using PropertyViewingSlots.Api.Configuration;
using PropertyViewingSlots.Api.Infrastructure;

namespace PropertyViewingSlots.Tests;

[TestFixture]
public sealed class ViewingServiceTests
{
    private static readonly DateTimeOffset FixedNow = Utc(2025, 1, 2, 12, 0);

    [TestCase(9, 0)]
    [TestCase(19, 0)]
    public void Book_AllowsConfiguredUkFirstAndFinalBookableSlot(int hour, int minute)
    {
        var result = CreateService().Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 1, 3, hour, minute)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BookingStatus.Created));
            Assert.That(result.Booking!.StartTimeUtc.Offset, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [TestCase(8, 30)]
    [TestCase(19, 45)]
    [TestCase(20, 0)]
    public void Book_RejectsSlotsOutsideConfiguredUkHoursOrBoundary(int hour, int minute)
    {
        var result = CreateService().Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 1, 3, hour, minute)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
    }

    [Test]
    public void Book_RejectsConfiguredClosingTime()
    {
        var result = CreateService().Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 1, 3, 19, 30)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
            Assert.That(result.Errors["startTime"], Does.Contain("The property is closed at 19:30 in UK."));
        });
    }

    [Test]
    public void Book_RequiresUtcTimestamp()
    {
        var nonUtcStart = new DateTimeOffset(2025, 1, 3, 10, 0, 0, TimeSpan.FromHours(1));
        var result = CreateService().Book(new BookViewingCommand("property-uk-123", "user-1", nonUtcStart));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
        Assert.That(result.Errors["startTime"], Does.Contain("Start time must use a UTC offset of +00:00."));
    }

    [Test]
    public void Book_ReturnsAllStartTimeValidationErrors()
    {
        var result = CreateService().Book(new BookViewingCommand(
            "property-uk-123",
            "user-1",
            new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.FromHours(1))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
            Assert.That(result.Errors["startTime"], Does.Contain("Start time must use a UTC offset of +00:00."));
            Assert.That(result.Errors["startTime"], Does.Contain("Start time must be in the future."));
        });
    }

    [Test]
    public void Book_RejectsSubMinuteTickWithoutCreatingAnotherBooking()
    {
        var repository = new InMemoryViewingRepository();
        var service = CreateService(repository: repository);
        var slot = Utc(2025, 1, 3, 10, 0);

        var initialBooking = service.Book(new BookViewingCommand("property-uk-123", "user-1", slot));
        var subMinuteBooking = service.Book(new BookViewingCommand("property-uk-123", "user-2", slot.AddTicks(1)));

        Assert.Multiple(() =>
        {
            Assert.That(initialBooking.Status, Is.EqualTo(BookingStatus.Created));
            Assert.That(subMinuteBooking.Status, Is.EqualTo(BookingStatus.ValidationFailed));
            Assert.That(
                subMinuteBooking.Errors["startTime"],
                Does.Contain("Start time must be on a 30-minute boundary in the property's local time zone."));
            Assert.That(repository.GetByProperty("property-uk-123"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Book_RejectsPastSlot()
    {
        var result = CreateService().Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 1, 2, 11, 30)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
        Assert.That(result.Errors["startTime"], Does.Contain("Start time must be in the future."));
    }

    [Test]
    public void Book_ValidatesUkBusinessHoursUsingBritishSummerTime()
    {
        var result = CreateService(Utc(2025, 6, 1, 12, 0))
            .Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 6, 10, 8, 0)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.Created));
    }

    [Test]
    public void Book_ValidatesVietnamBusinessHoursUsingConfiguredLocation()
    {
        var result = CreateService(Utc(2025, 6, 1, 12, 0))
            .Book(new BookViewingCommand("property-vn-123", "user-1", Utc(2025, 6, 10, 2, 0)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.Created));
    }

    [Test]
    public void Book_ValidatesUsEastBusinessHoursUsingConfiguredLocation()
    {
        var result = CreateService(Utc(2025, 6, 1, 12, 0))
            .Book(new BookViewingCommand("property-us-east-123", "user-1", Utc(2025, 6, 10, 13, 0)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.Created));
    }

    [Test]
    public void Book_RejectsUtcTimeThatFallsOutsidePropertyLocalHours()
    {
        var result = CreateService(Utc(2025, 6, 1, 12, 0))
            .Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 6, 10, 7, 0)));

        Assert.That(result.Status, Is.EqualTo(BookingStatus.ValidationFailed));
    }

    [Test]
    public void Book_ReturnsConflictForDuplicatePropertySlot_ButAllowsAnotherProperty()
    {
        var service = CreateService();
        var slot = Utc(2025, 1, 3, 10, 0);

        var initialBooking = service.Book(new BookViewingCommand("property-uk-123", "user-1", slot));
        var duplicateBooking = service.Book(new BookViewingCommand("property-uk-123", "user-2", slot));
        var otherPropertyBooking = service.Book(new BookViewingCommand("property-vn-123", "user-2", Utc(2025, 1, 3, 2, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(initialBooking.Status, Is.EqualTo(BookingStatus.Created));
            Assert.That(duplicateBooking.Status, Is.EqualTo(BookingStatus.Conflict));
            Assert.That(otherPropertyBooking.Status, Is.EqualTo(BookingStatus.Created));
        });
    }

    [Test]
    public void Search_ReturnsUtcSlotsAndExcludesBookedSlot()
    {
        var service = CreateService();
        var date = new DateOnly(2025, 1, 3);

        var initialSearch = service.SearchAvailableSlots(new SearchAvailableSlotsQuery("property-uk-123", date, date));
        service.Book(new BookViewingCommand("property-uk-123", "user-1", Utc(2025, 1, 3, 10, 0)));
        var subsequentSearch = service.SearchAvailableSlots(new SearchAvailableSlotsQuery("property-uk-123", date, date));

        Assert.Multiple(() =>
        {
            Assert.That(initialSearch.Slots, Has.Count.EqualTo(21));
            Assert.That(initialSearch.Slots, Is.All.Matches<DateTimeOffset>(slot => slot.Offset == TimeSpan.Zero));
            Assert.That(subsequentSearch.Slots, Has.Count.EqualTo(20));
            Assert.That(subsequentSearch.Slots, Does.Not.Contain(Utc(2025, 1, 3, 10, 0)));
        });
    }

    [Test]
    public void Search_UsesPropertyLocalDateAndHoursForUsEast()
    {
        var result = CreateService(Utc(2025, 6, 1, 12, 0)).SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-us-east-123",
            new DateOnly(2025, 6, 10),
            new DateOnly(2025, 6, 10)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Slots, Has.Count.EqualTo(21));
            Assert.That(result.Slots.First(), Is.EqualTo(Utc(2025, 6, 10, 13, 0)));
            Assert.That(result.Slots.Last(), Is.EqualTo(Utc(2025, 6, 10, 23, 0)));
        });
    }

    [Test]
    public void Search_AllowsAnInclusiveThirtyOneDayRange()
    {
        var from = new DateOnly(2025, 1, 3);
        var result = CreateService().SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-uk-123", from, from.AddDays(30)));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Search_RejectsRangeLongerThanThirtyOneInclusiveDays()
    {
        var from = new DateOnly(2025, 1, 3);
        var result = CreateService().SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-uk-123", from, from.AddDays(31)));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors["dateRange"], Does.Contain("The date range must not exceed 31 days."));
        });
    }

    [Test]
    public void Search_HandlesClosingTimeAtTwentyThreeThirtyWithoutWrapping()
    {
        var options = CreateOptions(ukFirstSlotStart: "23:00", ukLastSlotStart: "23:30");
        var result = CreateService(options: options).SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-uk-123", new DateOnly(2025, 1, 3), new DateOnly(2025, 1, 3)));

        Assert.That(result.Slots, Is.EqualTo(new[] { Utc(2025, 1, 3, 23, 0) }));
    }

    [Test]
    public void Search_SkipsInvalidDstLocalTimesAndUsesDeterministicAmbiguousTimes()
    {
        var options = CreateOptions(ukFirstSlotStart: "01:00", ukLastSlotStart: "01:30");
        var service = CreateService(Utc(2025, 3, 1, 12, 0), options);

        var springForward = service.SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-uk-123", new DateOnly(2025, 3, 30), new DateOnly(2025, 3, 30)));
        var autumnFallback = service.SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "property-uk-123", new DateOnly(2025, 10, 26), new DateOnly(2025, 10, 26)));

        Assert.Multiple(() =>
        {
            Assert.That(springForward.Slots, Is.Empty);
            Assert.That(autumnFallback.Slots, Is.EqualTo(new[]
            {
                Utc(2025, 10, 26, 1, 0)
            }));
        });
    }

    [Test]
    public void UnknownPropertyMappingFailsBookingAndSearch()
    {
        var service = CreateService();
        var booking = service.Book(new BookViewingCommand("unknown-property", "user-1", Utc(2025, 1, 3, 10, 0)));
        var search = service.SearchAvailableSlots(new SearchAvailableSlotsQuery(
            "unknown-property", new DateOnly(2025, 1, 3), new DateOnly(2025, 1, 3)));

        Assert.Multiple(() =>
        {
            Assert.That(booking.Status, Is.EqualTo(BookingStatus.ValidationFailed));
            Assert.That(booking.Errors.Keys, Does.Contain("propertyId"));
            Assert.That(search.IsValid, Is.False);
            Assert.That(search.Errors.Keys, Does.Contain("propertyId"));
        });
    }

    [Test]
    public void Search_RejectsBlankPropertyAndReversedDateRange()
    {
        var result = CreateService().SearchAvailableSlots(new SearchAvailableSlotsQuery(
            " ", new DateOnly(2025, 1, 4), new DateOnly(2025, 1, 3)));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Keys, Does.Contain("propertyId"));
            Assert.That(result.Errors.Keys, Does.Contain("dateRange"));
        });
    }

    [Test]
    public void OptionsValidator_ReportsInvalidLocationSettingsAndMappings()
    {
        var options = CreateOptions();
        options.Locations["Broken"] = new ViewingLocationOptions
        {
            TimeZoneId = "Not/A-TimeZone",
            FirstSlotStart = "09:30",
            LastSlotStart = "09:00"
        };
        options.Locations["Bad-Time"] = new ViewingLocationOptions
        {
            TimeZoneId = "Europe/London",
            FirstSlotStart = "09:15",
            LastSlotStart = "19:30"
        };
        options.PropertyLocations["broken-property"] = "Missing";

        var result = new ViewingSlotsOptionsValidator().Validate(null, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failed, Is.True);
            Assert.That(result.Failures, Has.Some.Contains("cannot be resolved"));
            Assert.That(result.Failures, Has.Some.Contains("30-minute boundary"));
            Assert.That(result.Failures, Has.Some.Contains("must not be after"));
            Assert.That(result.Failures, Has.Some.Contains("must reference a configured location"));
        });
    }

    [Test]
    public void OptionsValidator_RequiresAtLeastOneLocation()
    {
        var result = new ViewingSlotsOptionsValidator().Validate(null, new ViewingSlotsOptions());

        Assert.That(result.Failed, Is.True);
        Assert.That(result.Failures, Has.Some.Contains("must define at least one location"));
    }

    private static ViewingService CreateService(
        DateTimeOffset? now = null,
        ViewingSlotsOptions? options = null,
        InMemoryViewingRepository? repository = null)
    {
        var provider = new PropertyLocationProvider(Options.Create(options ?? CreateOptions()));
        return new ViewingService(repository ?? new InMemoryViewingRepository(), provider, new FixedTimeProvider(now ?? FixedNow));
    }

    private static ViewingSlotsOptions CreateOptions(
        string ukFirstSlotStart = "09:00",
        string ukLastSlotStart = "19:30") =>
        new()
        {
            Locations = new Dictionary<string, ViewingLocationOptions>(StringComparer.Ordinal)
            {
                ["UK"] = new() { TimeZoneId = "Europe/London", FirstSlotStart = ukFirstSlotStart, LastSlotStart = ukLastSlotStart },
                ["VN"] = new() { TimeZoneId = "Asia/Ho_Chi_Minh", FirstSlotStart = "09:00", LastSlotStart = "19:30" },
                ["US-East"] = new() { TimeZoneId = "America/New_York", FirstSlotStart = "09:00", LastSlotStart = "19:30" }
            },
            PropertyLocations = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["property-uk-123"] = "UK",
                ["property-vn-123"] = "VN",
                ["property-us-east-123"] = "US-East"
            }
        };

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
