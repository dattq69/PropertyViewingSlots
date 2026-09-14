using PropertyViewingSlots.Api.Application;
using PropertyViewingSlots.Api.Configuration;
using PropertyViewingSlots.Api.Contracts;
using PropertyViewingSlots.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<SwaggerExamplesOperationFilter>());
builder.Services.AddSingleton<IValidateOptions<ViewingSlotsOptions>, ViewingSlotsOptionsValidator>();
builder.Services.AddOptions<ViewingSlotsOptions>()
    .Bind(builder.Configuration.GetSection(ViewingSlotsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IViewingRepository, InMemoryViewingRepository>();
builder.Services.AddSingleton<IPropertyLocationProvider, PropertyLocationProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IViewingService, ViewingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/viewings", (
    BookViewingRequest? request,
    IViewingService viewingService) =>
{
    if (request is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["A booking request is required."]
        });
    }

    var result = viewingService.Book(new BookViewingCommand(
        request.PropertyId,
        request.UserId,
        request.StartTime));

    return result.Status switch
    {
        BookingStatus.Created => Results.Created(
            $"/api/viewings/{result.Booking!.Id}",
            ViewingMapper.ToResponse(result.Booking)),
        BookingStatus.Conflict => Results.Conflict(new ProblemDetails
        {
            Title = "Viewing slot is no longer available.",
            Detail = "This property already has a booking for the requested slot.",
            Status = StatusCodes.Status409Conflict
        }),
        _ => Results.ValidationProblem(new Dictionary<string, string[]>(result.Errors))
    };
})
.WithName("BookViewing");

app.MapGet("/api/viewings/{bookingId:guid}", (
    Guid bookingId,
    IViewingRepository viewingRepository) =>
{
    var booking = viewingRepository.GetById(bookingId);
    return booking is null
        ? Results.NotFound()
        : Results.Ok(ViewingMapper.ToResponse(booking));
})
.WithName("GetViewing");

app.MapGet("/api/properties/{propertyId}/available-viewing-slots", (
    string propertyId,
    DateOnly from,
    DateOnly to,
    IViewingService viewingService) =>
{
    var result = viewingService.SearchAvailableSlots(new SearchAvailableSlotsQuery(propertyId, from, to));

    return result.IsValid
        ? Results.Ok(new AvailableViewingSlotsResponse(
            result.PropertyId!,
            from,
            to,
            result.Slots.Select(ViewingMapper.ToResponse).ToArray()))
        : Results.ValidationProblem(new Dictionary<string, string[]>(result.Errors));
})
.WithName("SearchAvailableViewingSlots");

app.Run();

public partial class Program;
