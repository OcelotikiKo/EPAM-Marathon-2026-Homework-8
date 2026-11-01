
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;

namespace GreenTasty.Aqa.Fixtures;

public sealed class GroupOrderFixture : IAsyncDisposable
{
    private readonly RestaurantApiClient _publicApi;

    public RestaurantApiClient OwnerApi { get; }
    public string OwnerToken { get; }
    public string ReservationId { get; }
    public string GroupId { get; }

    private GroupOrderFixture(
        RestaurantApiClient publicApi,
        RestaurantApiClient ownerApi,
        string token,
        string reservationId,
        string groupId)
    {
        _publicApi = publicApi;
        OwnerApi = ownerApi;
        OwnerToken = token;
        ReservationId = reservationId;
        GroupId = groupId;
    }

    public static async Task<GroupOrderFixture> CreateAsync()
    {
        var publicApi = new RestaurantApiClient();
        RestaurantApiClient? ownerApi = null;
        string? reservationId = null;

        try
        {
            string token = await publicApi.SignInAsync(
                TestSettings.OwnerEmail,
                TestSettings.OwnerPassword);

            ownerApi = new RestaurantApiClient(token);

            // A future date with a variable offset.

reservationId = await CreateReservationWithRetriesAsync(
    publicApi, ownerApi);


            using var ownerInfo =
                await ownerApi.GetOwnerSessionAsync(
                    reservationId);

            string groupId = RestaurantApiClient.ReadString(
                ownerInfo.RootElement, "groupReservationId");

            return new GroupOrderFixture(
                publicApi,
                ownerApi,
                token,
                reservationId,
                groupId);
        }
        catch
        {
            if (ownerApi != null && reservationId != null)
            {
                try
                {
                    await ownerApi.CancelReservationAsync(
                        reservationId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Cleanup warning: {ex.Message}");
                }
            }

            ownerApi?.Dispose();
            publicApi.Dispose();
            throw;
        }
    }



private static async Task<string> CreateReservationWithRetriesAsync(
    RestaurantApiClient publicApi,
    RestaurantApiClient ownerApi)
{
    const int maxAttempts = 24;
    int attempts = 0;

    // Use different future dates and time slots.
    var candidateSlots = Enumerable.Range(3, 24)
        .SelectMany(day =>
            new[] { 11, 15, 19 }.Select(hour =>
                DateTime.UtcNow.Date
                    .AddDays(day)
                    .AddHours(hour)))
        .OrderBy(_ => Random.Shared.Next())
        .ToArray();

    foreach (var startUtc in candidateSlots)
    {
        using var availableTables =
            await publicApi.GetAvailableTablesAsync(
                startUtc, 4);

        if (availableTables.RootElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Available tables response is not an array.");
        }

        var tableIds = availableTables.RootElement
            .EnumerateArray()
            .Where(t => t.ValueKind == JsonValueKind.Object)
            .Select(t => RestaurantApiClient.ReadString(t, "id"))
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        foreach (string tableId in tableIds)
        {
            attempts++;

            try
            {
                using var reservation =
                    await ownerApi.CreateReservationAsync(
                        tableId,
                        startUtc,
                        startUtc.AddMinutes(90),
                        4);

                string reservationId =
                    RestaurantApiClient.ReadString(
                        reservation.RootElement, "id");

                Console.WriteLine(
                    $"Test reservation created: {reservationId}");

                return reservationId;
            }
            catch (HttpRequestException ex)
                when (ex.Message.Contains(
                    "overlaps with an existing reservation",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Slot conflict: {startUtc:yyyy-MM-dd HH:mm}, " +
                    $"table {tableId}. Trying another slot.");
            }

            if (attempts >= maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Unable to create test reservation " +
                    $"after {maxAttempts} attempts.");
            }
        }
    }

    throw new InvalidOperationException(
        "No suitable reservation slot was found.");
}


public async Task<string> AddGuestAsync()
{
    var guest = await AddGuestWithTokenAsync();
    return guest.SessionId;
}

public async Task<(string SessionId, string SessionToken)>
    AddGuestWithTokenAsync()
{
    string guestName =
        "AQA Guest " + Guid.NewGuid().ToString("N")[..8];

    using var response =
        await _publicApi.CreateGuestSessionAsync(
            GroupId, guestName);

    return (
        RestaurantApiClient.ReadString(
            response.RootElement, "SessionId"),
        RestaurantApiClient.ReadString(
            response.RootElement, "SessionToken")
    );
}



public async ValueTask DisposeAsync()
{
    try
    {
        await OwnerApi.CancelReservationAsync(ReservationId);
        Console.WriteLine(
            $"Test reservation {ReservationId} cancelled.");
    }
    finally
    {
        OwnerApi.Dispose();
        _publicApi.Dispose();
    }
}

}
