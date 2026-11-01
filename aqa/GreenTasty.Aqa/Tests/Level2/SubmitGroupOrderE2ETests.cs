
using System.Net;
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;
using GreenTasty.Aqa.Fixtures;
using GreenTasty.Aqa.Pages;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level2;

[TestFixture]
[Category("Level2")]
[Category("E2E")]
public class SubmitGroupOrderE2ETests
{
    [Test]
    public async Task Owner_ShouldSubmitOnlyConfirmedGuestOrder()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        var (guest1Id, guest1Token) =
            await fixture.AddGuestWithTokenAsync();

        var (guest2Id, guest2Token) =
            await fixture.AddGuestWithTokenAsync();

        using var guest1Api =
            new RestaurantApiClient(guest1Token);

        using var guest2Api =
            new RestaurantApiClient(guest2Token);

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

        Assert.That(
            dishes.Length,
            Is.GreaterThanOrEqualTo(2));

        string dish1Id =
            dishes[0].GetProperty("id").GetString()!;

        string dish2Id =
            dishes[1].GetProperty("id").GetString()!;

        // Prepare an existing Owner pre-order.
        using var initialOrder =
            await fixture.OwnerApi.CreatePreOrderAsync(
                fixture.ReservationId, dish1Id, 1);

        // Guest 1: two portions, confirmed.
        await guest1Api.AddGuestDishAsync(
            guest1Id, dish1Id, 2);

        await guest1Api.SubmitGuestCartAsync(
            guest1Id);

        // Guest 2: one portion, not confirmed.
        await guest2Api.AddGuestDishAsync(
            guest2Id, dish2Id, 1);

        using var loginApi =
            new RestaurantApiClient();

        var (idToken, accessToken) =
            await loginApi.SignInTokensAsync(
                TestSettings.OwnerEmail,
                TestSettings.OwnerPassword);

        using var playwright =
            await Playwright.CreateAsync();

        var browser =
            await playwright.Chromium.LaunchAsync(
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
                    await ownerPage.OpenAsync(
                        fixture.ReservationId);

                    Assert.That(
                        await ownerPage.SubmitButton.IsEnabledAsync(),
                        Is.True);

                    // Submit through the real Angular UI.
                    await ownerPage.SubmitAsync();

                    Assert.That(
                        await ownerPage.Status.InnerTextAsync(),
                        Does.Contain("Sent to Restaurant"));

                    // Verify actual persisted order in Backend.
                    using var order =
                        await fixture.OwnerApi.GetOrderAsync(
                            fixture.ReservationId);

                    var items = order.RootElement
                        .GetProperty("orderItems")
                        .EnumerateArray()
                        .ToArray();

                    var firstDish = items.Single(item =>
                        item.GetProperty("dish")
                            .GetProperty("id")
                            .GetString() == dish1Id);

                    Assert.Multiple(() =>
                    {
                        // Initial 1 + confirmed guest's 2.
                        Assert.That(
                            firstDish.GetProperty("quantity")
                                .GetInt32(),
                            Is.EqualTo(3));

                        // Unconfirmed guest is excluded.
                        Assert.That(
                            items.Any(item =>
                                item.GetProperty("dish")
                                    .GetProperty("id")
                                    .GetString() == dish2Id),
                            Is.False);

                        // SubmittedByCustomer = 3.
                        Assert.That(
                            order.RootElement.GetProperty("status")
                                .GetInt32(),
                            Is.EqualTo(3));
                    });

                    using var groupStatus =
                        await fixture.OwnerApi.GetGroupStatusAsync(
                            fixture.GroupId);

                    Assert.That(
                        groupStatus.RootElement
                            .GetProperty("status")
                            .GetString(),
                        Is.EqualTo("Submitted"));

                    // Duplicate submission must be rejected.
                    using var duplicate =
                        await fixture.OwnerApi.TrySubmitGroupAsync(
                            fixture.GroupId,
                            fixture.ReservationId);

                    Assert.That(
                        duplicate.StatusCode,
                        Is.EqualTo(HttpStatusCode.BadRequest));

                    Assert.That(
                        await ownerPage.SubmitButton.CountAsync(),
                        Is.Zero);
                }
                catch
                {
                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                artifacts,
                                "level2-submit-failure.png"),
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
