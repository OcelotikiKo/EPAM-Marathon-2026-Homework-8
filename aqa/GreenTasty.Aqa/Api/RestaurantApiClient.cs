
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GreenTasty.Aqa.Config;

namespace GreenTasty.Aqa.Api;

public sealed class RestaurantApiClient : IDisposable
{
    private readonly HttpClient _http;

    public RestaurantApiClient(string? token = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(TestSettings.ApiUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

public async Task<string> SignInAsync(
    string email, string password)
{
    var tokens = await SignInTokensAsync(email, password);
    return tokens.IdToken;
}

public async Task<(string IdToken, string AccessToken)>
    SignInTokensAsync(string email, string password)
{
    using var json = await SendJsonAsync(
        HttpMethod.Post, "signin",
        new { email, password });

    return (
        ReadString(json.RootElement, "idToken"),
        ReadString(json.RootElement, "accessToken")
    );
}


    public Task<HttpResponseMessage> GetLocationsAsync()
        => _http.GetAsync("locations");

    public Task<HttpResponseMessage> GetGroupSessionsAsync(
        string groupReservationId)
        => _http.GetAsync(
            $"guest/groups/{Uri.EscapeDataString(groupReservationId)}/sessions");

    public Task<JsonDocument> GetGroupSessionsDocumentAsync(
        string groupReservationId)
        => SendJsonAsync(
            HttpMethod.Get,
            $"guest/groups/{Uri.EscapeDataString(groupReservationId)}/sessions");

    public Task<JsonDocument> GetAvailableTablesAsync(
        DateTime startUtc, int capacity)
        => SendJsonAsync(
            HttpMethod.Get,
            $"available-tables?capacity={capacity}&startTime=" +
            Uri.EscapeDataString(startUtc.ToString("O")));

    public Task<JsonDocument> CreateReservationAsync(
        string tableId, DateTime startUtc,
        DateTime endUtc, int guestCount)
        => SendJsonAsync(
            HttpMethod.Post, "reservations",
            new
            {
                tableId,
                startTime = startUtc,
                endTime = endUtc,
                guestCount
            });

    public Task<JsonDocument> GetOwnerSessionAsync(
        string reservationId)
        => SendJsonAsync(
            HttpMethod.Get,
            $"reservations/{Uri.EscapeDataString(reservationId)}/guest-session-id");

    public Task<JsonDocument> CreateGuestSessionAsync(
        string groupReservationId, string guestName)
        => SendJsonAsync(
            HttpMethod.Post, "guest/sessions",
            new { groupReservationId, guestName });


public Task<JsonDocument> GetGuestMenuAsync(string sessionId)
{
    return SendJsonAsync(
        HttpMethod.Get,
        $"guest/sessions/{Uri.EscapeDataString(sessionId)}/menu");
}

public async Task AddGuestDishAsync(
    string sessionId, string dishId, int quantity)
{
    using var result = await SendJsonAsync(
        HttpMethod.Post,
        $"guest/sessions/{Uri.EscapeDataString(sessionId)}/dishes",
        new { dishId, quantity });
}

public async Task SubmitGuestCartAsync(string sessionId)
{
    using var result = await SendJsonAsync(
        HttpMethod.Post,
        $"guest/sessions/{Uri.EscapeDataString(sessionId)}/submit",
        new { });
}


public Task<JsonDocument> CreatePreOrderAsync(
    string reservationId, string dishId, int quantity)
{
    return SendJsonAsync(
        HttpMethod.Post,
        "orders",
        new
        {
            reservationId,
            orderItems = new[]
            {
                new { dishId, quantity }
            }
        });
}

public Task<JsonDocument> GetOrderAsync(
    string reservationId)
{
    return SendJsonAsync(
        HttpMethod.Get,
        $"orders/{Uri.EscapeDataString(reservationId)}");
}

public Task<JsonDocument> GetGroupStatusAsync(
    string groupReservationId)
{
    return SendJsonAsync(
        HttpMethod.Get,
        $"guest/groups/{Uri.EscapeDataString(groupReservationId)}/status");
}

public Task<HttpResponseMessage> TrySubmitGroupAsync(
    string groupReservationId, string reservationId)
{
    return _http.PostAsJsonAsync(
        $"group-reservations/{Uri.EscapeDataString(groupReservationId)}/submit",
        new { reservationId });
}


public Task<HttpResponseMessage> DeleteGroupGuestAsync(
    string groupReservationId,
    string guestSessionId)
{
    return _http.DeleteAsync(
        $"guest/groups/{Uri.EscapeDataString(groupReservationId)}" +
        $"/sessions/{Uri.EscapeDataString(guestSessionId)}");
}


    public async Task CancelReservationAsync(
        string reservationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete, "reservations")
        {
            Content = JsonContent.Create(new { id = reservationId })
        };

        using var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Reservation cleanup failed: HTTP " +
                $"{(int)response.StatusCode}: {body}");
        }
    }

    private async Task<JsonDocument> SendJsonAsync(
        HttpMethod method, string path, object? payload = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (payload != null)
            request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"{method} {path}: HTTP " +
                $"{(int)response.StatusCode}: {body}");

        return JsonDocument.Parse(body);
    }

    public static string ReadString(
        JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                property.Name, propertyName,
                StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetString()
                    ?? throw new InvalidOperationException(
                        $"Empty property: {propertyName}");
            }
        }

        throw new InvalidOperationException(
            $"Missing property: {propertyName}");
    }

    public void Dispose() => _http.Dispose();
}
