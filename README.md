# Property Viewing Slots

A deliberately small .NET 8 REST API for booking and finding 30-minute property-viewing slots. It uses ASP.NET Core's built-in dependency injection, Swagger for manual exploration, an in-memory repository, and NUnit tests. Viewing hours belong to the property's configured location; all API timestamps and stored bookings use UTC.

## Run locally

Prerequisite: .NET 8 SDK.

```powershell
dotnet restore
dotnet run --project src/PropertyViewingSlots.Api
```

Open the Swagger UI at the URL printed by ASP.NET Core, normally `http://localhost:5xxx/swagger`.

Run the tests with:

```powershell
dotnet test
```

## Location and business-hours configuration

`src/PropertyViewingSlots.Api/appsettings.json` maps each property to a location. A location supplies an IANA time-zone ID, its local first bookable slot start, and its exclusive closing time.

```json
"Locations": {
  "UK": { "TimeZoneId": "Europe/London", "FirstSlotStart": "09:00", "LastSlotStart": "20:00" },
  "VN": { "TimeZoneId": "Asia/Ho_Chi_Minh", "FirstSlotStart": "08:00", "LastSlotStart": "22:00" },
  "US-East": { "TimeZoneId": "America/New_York", "FirstSlotStart": "09:00", "LastSlotStart": "21:00" }
}
```

The initial mappings are `property-uk-123` (UK), `property-vn-123` (Vietnam), and `property-us-east-123` (US-East). Add a location and map its property IDs before accepting bookings for a new market. Use separate US entries such as `US-Central` or `US-Pacific`; a generic `US` time zone would be ambiguous.

Configuration is validated during startup. The service intentionally does not use the deployment server's local time zone, because a UK property's business hours must remain correct when the API runs in a UTC container or another region.

## API

### Book a viewing

`POST /api/viewings`

```json
{
  "propertyId": "property-uk-123",
  "userId": "user-456",
  "startTime": "2026-06-10T08:00:00Z"
}
```

`startTime` must be UTC. It is converted to the property's configured local time to validate opening hours: for example, `08:00Z` is 09:00 in the UK during BST. A successful response returns `startTimeUtc`. Invalid input, an unmapped property, non-UTC input, a past slot, or a time outside local business hours returns `400 Bad Request`; a booking already held by the property for that UTC slot returns `409 Conflict`.

### Find available slots

`GET /api/properties/property-uk-123/available-viewing-slots?from=2026-06-10&to=2026-06-11`

`from` and `to` are inclusive calendar dates in the property's configured location and may span at most 31 days. The response contains unbooked, future 30-minute slots from the configured opening time up to, but not including, the closing time, represented as `startTimeUtc` values. For example, a 09:00 UK BST slot is `08:00Z`, Vietnam is `02:00Z`, and US-East summer time is `13:00Z`.

## Next Steps

- Replace the in-memory property-location mapping and booking store with a property service plus EF Core/PostgreSQL, using a unique `(PropertyId, StartTimeUtc)` index and transaction/constraint handling as the final cross-instance double-booking protection.
- Add authentication and authorization, rate limiting, HTTPS enforcement, audit logging, secret management, and operational monitoring.
- Establish domain rules for property-specific hours, blackout dates, agent availability, multiple capacities, lead times, cancellations, and rescheduling.
- Add paging, asynchronous persistence, structured logs/metrics/traces, and measured caching where needed.

## AI usage

ChatGPT/Codex was used to help plan the implementation, review API and test cases, and draft documentation. The submitted design and code were reviewed, understood, and validated by me.
