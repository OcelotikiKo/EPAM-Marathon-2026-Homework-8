
using System.Globalization;
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;
using GreenTasty.Aqa.Fixtures;
using GreenTasty.Aqa.Pages;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
[Category("Level1")]
[Category("E2E")]
public class GroupOrderDishE2ETests
{
    [Test]
    public async Task GuestDish_ShouldUpdateOwnerViewAfterSubmission()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        var (guestId, guestToken) =
            await fixture.AddGuestWithTokenAsync();

        using var guestApi =
            new RestaurantApiClient(guestToken);

        using var menu =
            await guestApi.GetGuestMenuAsync(guestId);

        var dishes = menu.RootElement
            .GetProperty("dishes")
            .EnumerateObject()
            .SelectMany(category =>
                category.Value.EnumerateArray())
            .Where(d =>
                d.GetProperty("selectable").GetBoolean() &&
                d.GetProperty("price").GetDecimal() > 0)
            .ToArray();

        Assert.That(dishes, Is.Not.Empty,
            "No available dishes in the guest menu.");

        var dish = dishes[0];

        string dishId = dish.GetProperty("id").GetString()!;
        string dishName = dish.GetProperty("name").GetString()!;
        decimal price = dish.GetProperty("price").GetDecimal();

        const int quantity = 2;
        decimal expectedTotal = price * quantity;

        await guestApi.AddGuestDishAsync(
            guestId, dishId, quantity);

        using var sessions =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var guest = sessions.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .Single(p =>
                p.GetProperty("Id").GetString() == guestId);

        string guestName =
            guest.GetProperty("Name").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(
                guest.GetProperty("IsSubmitted").GetBoolean(),
                Is.False);

            Assert.That(
                guest.GetProperty("Total").GetDecimal(),
                Is.EqualTo(expectedTotal));

            Assert.That(
                sessions.RootElement.GetProperty("Total")
                    .GetDecimal(),
                Is.EqualTo(expectedTotal));
        });

        using var loginApi = new RestaurantApiClient();

        var (idToken, accessToken) =
            await loginApi.SignInTokensAsync(
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
                    await ownerPage.OpenAsync(
                        fixture.ReservationId);

                    var guestCard =
                        ownerPage.GuestCard(guestName);

                    string before =
                        await guestCard.InnerTextAsync();

                    string totalBefore =
                        await ownerPage.GetGrandTotalAsync();

                    // An unsubmitted cart must not contribute
                    // to the consolidated pre-order.
                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            before, Does.Contain(dishName));

                        Assert.That(
                            before, Does.Contain("Still choosing"));

                        Assert.That(
                            totalBefore, Does.Contain("$0.00"));
                    });

                    // Guest confirms their personal cart.
                    await guestApi.SubmitGuestCartAsync(guestId);

                    // Owner refreshes the group view.
                    await page.GetByRole(
                        AriaRole.Button,
                        new PageGetByRoleOptions
                        {
                            Name = "Refresh",
                            Exact = true
                        }).ClickAsync();

                    await guestCard
                        .Locator(".chip.submitted")
                        .WaitForAsync();

                    string after =
                        await guestCard.InnerTextAsync();

                    string totalAfter =
                        await ownerPage.GetGrandTotalAsync();

                    string expectedPrice =
                        expectedTotal.ToString(
                            "C",
                            CultureInfo.GetCultureInfo("en-US"));

                    string consolidated =
                        await page.Locator(
                            "section.consolidated")
                            .InnerTextAsync();

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            after, Does.Contain(dishName));

                        Assert.That(
                            after, Does.Contain("Submitted"));

                        Assert.That(
                            after, Does.Contain(expectedPrice));

                        Assert.That(
                            consolidated,
                            Does.Contain(dishName));

                        Assert.That(
                            consolidated,
                            Does.Contain("× 2"));

                        Assert.That(
                            totalAfter,
                            Does.Contain(expectedPrice));
                    });
                }
                catch
                {
                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                artifacts,
                                "level1-dish-failure.png"),
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
