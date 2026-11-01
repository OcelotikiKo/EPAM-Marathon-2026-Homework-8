
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
public class MultipleGuestsE2ETests
{
    [Test]
    public async Task Owner_ShouldSeeTwoGuestsAndCorrectTotal()
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

        Assert.That(dishes.Length, Is.GreaterThanOrEqualTo(2),
            "At least two available dishes are required.");

        var dish1 = dishes[0];
        var dish2 = dishes[1];

        string dish1Id = dish1.GetProperty("id").GetString()!;
        string dish2Id = dish2.GetProperty("id").GetString()!;

        string dish1Name = dish1.GetProperty("name").GetString()!;
        string dish2Name = dish2.GetProperty("name").GetString()!;

        decimal price1 = dish1.GetProperty("price").GetDecimal();
        decimal price2 = dish2.GetProperty("price").GetDecimal();

        decimal submittedTotal = price1 * 2;
        decimal pendingTotal = price2;
        decimal allGuestsTotal = submittedTotal + pendingTotal;

        // Guest 1: two dishes, submitted.
        await guest1Api.AddGuestDishAsync(
            guest1Id, dish1Id, 2);

        await guest1Api.SubmitGuestCartAsync(guest1Id);

        // Guest 2: one dish, not submitted.
        await guest2Api.AddGuestDishAsync(
            guest2Id, dish2Id, 1);

        using var snapshot =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var participants = snapshot.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        var guest1 = participants.Single(p =>
            p.GetProperty("Id").GetString() == guest1Id);

        var guest2 = participants.Single(p =>
            p.GetProperty("Id").GetString() == guest2Id);

        string guest1Name =
            guest1.GetProperty("Name").GetString()!;

        string guest2Name =
            guest2.GetProperty("Name").GetString()!;

        Assert.Multiple(() =>
        {
            Assert.That(participants.Length, Is.EqualTo(3));

            Assert.That(
                guest1.GetProperty("IsSubmitted").GetBoolean(),
                Is.True);

            Assert.That(
                guest2.GetProperty("IsSubmitted").GetBoolean(),
                Is.False);

            Assert.That(
                guest1.GetProperty("Total").GetDecimal(),
                Is.EqualTo(submittedTotal));

            Assert.That(
                guest2.GetProperty("Total").GetDecimal(),
                Is.EqualTo(pendingTotal));

            Assert.That(
                snapshot.RootElement.GetProperty("Total")
                    .GetDecimal(),
                Is.EqualTo(allGuestsTotal));
        });

        // Browser E2E.
        using var loginApi = new RestaurantApiClient();

        var (idToken, accessToken) =
            await loginApi.SignInTokensAsync(
                TestSettings.OwnerEmail,
                TestSettings.OwnerPassword);

        using var playwright = await Playwright.CreateAsync();

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

                try
                {
                    await ownerPage.OpenAsync(fixture.ReservationId);

                    string guest1Text =
                        await ownerPage.GuestCard(guest1Name)
                            .InnerTextAsync();

                    string guest2Text =
                        await ownerPage.GuestCard(guest2Name)
                            .InnerTextAsync();

                    string consolidated =
                        await page.Locator("section.consolidated")
                            .InnerTextAsync();

                    string displayedTotal =
                        await ownerPage.GetGrandTotalAsync();

                    string expectedTotal =
                        submittedTotal.ToString(
                            "C",
                            CultureInfo.GetCultureInfo("en-US"));

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            ownerPage.Participants.CountAsync()
                                .GetAwaiter().GetResult(),
                            Is.EqualTo(3));

                        Assert.That(
                            guest1Text, Does.Contain("Submitted"));

                        Assert.That(
                            guest1Text, Does.Contain(dish1Name));

                        Assert.That(
                            guest2Text, Does.Contain("Still choosing"));

                        Assert.That(
                            guest2Text, Does.Contain(dish2Name));

                        Assert.That(
                            consolidated, Does.Contain(dish1Name));

                        Assert.That(
                            consolidated, Does.Not.Contain(dish2Name));

                        Assert.That(
                            displayedTotal, Does.Contain(expectedTotal));
                    });
                }
                catch
                {
                    string directory = Path.Combine(
                        TestContext.CurrentContext.WorkDirectory,
                        "artifacts");

                    Directory.CreateDirectory(directory);

                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                directory,
                                "level1-multiple-guests-failure.png"),
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
