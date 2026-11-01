
using System.Net;
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Fixtures;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level2;

[TestFixture]
[Category("Level2")]
[Category("Negative")]
public class SubmitGroupNegativeTests
{
    [Test]
    public async Task Owner_CannotSubmit_UnconfirmedGuestCart()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        var (guestId, guestToken) =
            await fixture.AddGuestWithTokenAsync();

        using var guestApi =
            new RestaurantApiClient(guestToken);

        string dishId =
            await GetAvailableDishIdAsync(guestApi, guestId);

        // Create an initial order, but do not submit
        // the guest's personal cart.
        using var initialOrder =
            await fixture.OwnerApi.CreatePreOrderAsync(
                fixture.ReservationId, dishId, 1);

        await guestApi.AddGuestDishAsync(
            guestId, dishId, 2);

        using var response =
            await fixture.OwnerApi.TrySubmitGroupAsync(
                fixture.GroupId, fixture.ReservationId);

        string body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(
                body,
                Does.Contain("No submitted guest sessions"));
        });

        using var status =
            await fixture.OwnerApi.GetGroupStatusAsync(
                fixture.GroupId);

        Assert.That(
            status.RootElement.GetProperty("status").GetString(),
            Is.EqualTo("Open"));
    }

    [Test]
    public async Task Owner_CannotSubmit_WithoutExistingOrder()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        var (guestId, guestToken) =
            await fixture.AddGuestWithTokenAsync();

        using var guestApi =
            new RestaurantApiClient(guestToken);

        string dishId =
            await GetAvailableDishIdAsync(guestApi, guestId);

        await guestApi.AddGuestDishAsync(
            guestId, dishId, 1);

        await guestApi.SubmitGuestCartAsync(guestId);

        // No POST /orders request was made.
        using var response =
            await fixture.OwnerApi.TrySubmitGroupAsync(
                fixture.GroupId, fixture.ReservationId);

        string body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(
                body, Does.Contain("Order not found"));
        });
    }

    [Test]
    public async Task Owner_CannotSubmit_WrongReservationId()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        string wrongReservationId =
            Guid.NewGuid().ToString();

        using var response =
            await fixture.OwnerApi.TrySubmitGroupAsync(
                fixture.GroupId, wrongReservationId);

        string body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.BadRequest));

            Assert.That(
                body,
                Does.Contain(
                    "Reservation does not belong to this group"));
        });

        using var status =
            await fixture.OwnerApi.GetGroupStatusAsync(
                fixture.GroupId);

        Assert.That(
            status.RootElement.GetProperty("status").GetString(),
            Is.EqualTo("Open"));
    }

    private static async Task<string> GetAvailableDishIdAsync(
        RestaurantApiClient guestApi,
        string guestSessionId)
    {
        using var menu =
            await guestApi.GetGuestMenuAsync(guestSessionId);

        var dish = menu.RootElement
            .GetProperty("dishes")
            .EnumerateObject()
            .SelectMany(category =>
                category.Value.EnumerateArray())
            .FirstOrDefault(item =>
                item.GetProperty("selectable").GetBoolean() &&
                item.GetProperty("price").GetDecimal() > 0);

        Assert.That(
            dish.ValueKind,
            Is.EqualTo(JsonValueKind.Object),
            "No available dishes were found.");

        return dish.GetProperty("id").GetString()!;
    }
}
