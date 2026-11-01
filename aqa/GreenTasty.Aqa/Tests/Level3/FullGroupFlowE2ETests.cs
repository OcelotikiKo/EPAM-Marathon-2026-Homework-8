
using System.Net;
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;
using GreenTasty.Aqa.Fixtures;
using GreenTasty.Aqa.Pages;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level3;

[TestFixture]
[Category("Level3")]
[Category("E2E")]
public class FullGroupFlowE2ETests
{
    [Test]
    public async Task RemoveGuest_ThenSubmit_CorrectOrder()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        // 1. Join two guests.
        var (guest1Id, guest1Token) =
            await fixture.AddGuestWithTokenAsync();

        var (guest2Id, guest2Token) =
            await fixture.AddGuestWithTokenAsync();

        using var guest1Api =
            new RestaurantApiClient(guest1Token);

        using var guest2Api =
            new RestaurantApiClient(guest2Token);

        // 2. Select two available dishes.
        using var menu =
            await guest1Api.GetGuestMenuAsync(guest1Id);

        var dishes = menu.RootElement
            .GetProperty("dishes")
            .EnumerateObject()
            .SelectMany(c => c.Value.EnumerateArray())
            .Where(d =>
                d.GetProperty("selectable").GetBoolean() &&
                d.GetProperty("price").GetDecimal() > 0)
            .ToArray();

        Assert.That(dishes.Length, Is.GreaterThanOrEqualTo(2));

        string dish1Id =
            dishes[0].GetProperty("id").GetString()!;

        string dish2Id =
            dishes[1].GetProperty("id").GetString()!;

        decimal dish1Price =
            dishes[0].GetProperty("price").GetDecimal();

        // 3. Create the original Owner order.
        using var initialOrder =
            await fixture.OwnerApi.CreatePreOrderAsync(
                fixture.ReservationId, dish1Id, 1);

        // 4. Both guests submit their carts.
        await guest1Api.AddGuestDishAsync(
            guest1Id, dish1Id, 2);

        await guest1Api.SubmitGuestCartAsync(guest1Id);

        await guest2Api.AddGuestDishAsync(
            guest2Id, dish2Id, 1);

        await guest2Api.SubmitGuestCartAsync(guest2Id);

        using var before =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var beforeParticipants = before.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        Assert.That(beforeParticipants.Length, Is.EqualTo(3));

        string guest1Name = beforeParticipants.Single(p =>
            p.GetProperty("Id").GetString() == guest1Id)
            .GetProperty("Name").GetString()!;

        string guest2Name = beforeParticipants.Single(p =>
            p.GetProperty("Id").GetString() == guest2Id)
            .GetProperty("Name").GetString()!;

        // 5. Remove Guest 2.
        using var deletion =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, guest2Id);

        Assert.That(
            deletion.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        // 6. Verify persisted group data.
        using var updated =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var participants = updated.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(participants.Length, Is.EqualTo(2));

            Assert.That(
                participants.Any(p =>
                    p.GetProperty("Id").GetString() == guest1Id),
                Is.True);

            Assert.That(
                participants.Any(p =>
                    p.GetProperty("Id").GetString() == guest2Id),
                Is.False);

            Assert.That(
                participants.Any(p =>
                    p.GetProperty("Type").GetString() == "Owner"),
                Is.True);

            Assert.That(
                updated.RootElement.GetProperty("Total")
                    .GetDecimal(),
                Is.EqualTo(dish1Price * 2));
        });

        // 7. Authenticate Owner.
        using var authApi = new RestaurantApiClient();

        var (idToken, accessToken) =
            await authApi.SignInTokensAsync(
                TestSettings.OwnerEmail,
                TestSettings.OwnerPassword);

        using var playwright =
            await Playwright.CreateAsync();

        var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });

        try
        {
            var context = await browser.NewContextAsync();

            try
            {
                string authJson = JsonSerializer.Serialize(
                    new { idToken, accessToken });

                await context.AddInitScriptAsync(
                    script: "localStorage.setItem('auth', " +
                        JsonSerializer.Serialize(authJson) + ");");

                var page = await context.NewPageAsync();
                var ownerPage = new GroupOrderPage(page);

                string artifacts = Path.Combine(
                    TestContext.CurrentContext.WorkDirectory,
                    "artifacts");

                Directory.CreateDirectory(artifacts);

                try
                {
                    // 8. Check the Owner View.
                    await ownerPage.OpenAsync(
                        fixture.ReservationId);

                    Assert.That(
                        await ownerPage.Participants.CountAsync(),
                        Is.EqualTo(2));

                    Assert.That(
                        await ownerPage.OwnerCard.IsVisibleAsync(),
                        Is.True);

                    Assert.That(
                        await ownerPage.GuestCard(guest1Name)
                            .IsVisibleAsync(),
                        Is.True);

                    Assert.That(
                        await ownerPage.GuestCard(guest2Name)
                            .CountAsync(),
                        Is.Zero);

                    // 9. Submit the order using Angular.
                    await ownerPage.SubmitAsync();

                    Assert.That(
                        await ownerPage.Status.InnerTextAsync(),
                        Does.Contain("Sent to Restaurant"));

                    // 10. Verify the actual saved order.
                    using var order =
                        await fixture.OwnerApi.GetOrderAsync(
                            fixture.ReservationId);

                    var items = order.RootElement
                        .GetProperty("orderItems")
                        .EnumerateArray()
                        .ToArray();

                    var dish1 = items.Single(item =>
                        item.GetProperty("dish")
                            .GetProperty("id")
                            .GetString() == dish1Id);

                    Assert.Multiple(() =>
                    {
                        // Original order: 1
                        // Remaining Guest 1: 2
                        Assert.That(
                            dish1.GetProperty("quantity").GetInt32(),
                            Is.EqualTo(3));

                        // Removed Guest 2 must be excluded.
                        Assert.That(
                            items.Any(item =>
                                item.GetProperty("dish")
                                    .GetProperty("id")
                                    .GetString() == dish2Id),
                            Is.False);

                        Assert.That(
                            order.RootElement
                                .GetProperty("status").GetInt32(),
                            Is.EqualTo(3));
                    });

                    using var groupStatus =
                        await fixture.OwnerApi.GetGroupStatusAsync(
                            fixture.GroupId);

                    Assert.That(
                        groupStatus.RootElement
                            .GetProperty("status").GetString(),
                        Is.EqualTo("Submitted"));

                    Console.WriteLine(
                        "Full E2E flow completed successfully.");
                }
                catch
                {
                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                artifacts,
                                "level3-full-flow-failure.png"),
                            FullPage = true
                        });

                    throw;
                }
            }
            finally
            {
                await context.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
