
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
public class RemoveGuestE2ETests
{
    [Test]
    public async Task RemovingGuest_ShouldUpdateGroupAndOwnerView()
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

        var dish = menu.RootElement
            .GetProperty("dishes")
            .EnumerateObject()
            .SelectMany(c => c.Value.EnumerateArray())
            .First(d =>
                d.GetProperty("selectable").GetBoolean() &&
                d.GetProperty("price").GetDecimal() > 0);

        string dishId =
            dish.GetProperty("id").GetString()!;

        decimal price =
            dish.GetProperty("price").GetDecimal();

        // Guest 1: two portions, confirmed.
        await guest1Api.AddGuestDishAsync(
            guest1Id, dishId, 2);

        await guest1Api.SubmitGuestCartAsync(guest1Id);

        // Guest 2: one portion, not confirmed.
        await guest2Api.AddGuestDishAsync(
            guest2Id, dishId, 1);

        using var before =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var participantsBefore = before.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        string guest1Name = participantsBefore.Single(p =>
            p.GetProperty("Id").GetString() == guest1Id)
            .GetProperty("Name").GetString()!;

        string guest2Name = participantsBefore.Single(p =>
            p.GetProperty("Id").GetString() == guest2Id)
            .GetProperty("Name").GetString()!;

        Assert.That(participantsBefore.Length, Is.EqualTo(3));

        Assert.That(
            before.RootElement.GetProperty("Total").GetDecimal(),
            Is.EqualTo(price * 3));

        using var authApi = new RestaurantApiClient();

        var (idToken, accessToken) =
            await authApi.SignInTokensAsync(
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

                    Assert.That(
                        await ownerPage.Participants.CountAsync(),
                        Is.EqualTo(3));

                    Assert.That(
                        await ownerPage.GuestCard(guest1Name)
                            .IsVisibleAsync(),
                        Is.True);

                    // Remove the confirmed guest through API.
                    using var deletion =
                        await fixture.OwnerApi.DeleteGroupGuestAsync(
                            fixture.GroupId, guest1Id);

                    string deletionBody =
                        await deletion.Content.ReadAsStringAsync();

                    Assert.That(
                        deletion.StatusCode,
                        Is.EqualTo(HttpStatusCode.OK),
                        deletionBody);

                    // Verify persisted Backend state.
                    using var updated =
                        await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                            fixture.GroupId);

                    var participantsAfter = updated.RootElement
                        .GetProperty("Participants")
                        .EnumerateArray()
                        .ToArray();

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            participantsAfter.Length,
                            Is.EqualTo(2));

                        Assert.That(
                            participantsAfter.Any(p =>
                                p.GetProperty("Id").GetString()
                                    == guest1Id),
                            Is.False);

                        Assert.That(
                            participantsAfter.Any(p =>
                                p.GetProperty("Id").GetString()
                                    == guest2Id),
                            Is.True);

                        Assert.That(
                            participantsAfter.Any(p =>
                                p.GetProperty("Type").GetString()
                                    == "Owner"),
                            Is.True);

                        Assert.That(
                            updated.RootElement.GetProperty("Total")
                                .GetDecimal(),
                            Is.EqualTo(price));
                    });

                    // Refresh the real Angular Owner View.
                    await page.GetByRole(
                        AriaRole.Button,
                        new PageGetByRoleOptions
                        {
                            Name = "Refresh",
                            Exact = true
                        }).ClickAsync();

                    await ownerPage.GuestCard(guest1Name)
                        .WaitForAsync(new LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Hidden,
                            Timeout = 15000
                        });

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            ownerPage.Participants.CountAsync()
                                .GetAwaiter().GetResult(),
                            Is.EqualTo(2));

                        Assert.That(
                            ownerPage.OwnerCard.IsVisibleAsync()
                                .GetAwaiter().GetResult(),
                            Is.True);

                        Assert.That(
                            ownerPage.GuestCard(guest2Name)
                                .IsVisibleAsync()
                                .GetAwaiter().GetResult(),
                            Is.True);

                        // Guest 2 did not submit their cart.
                        Assert.That(
                            ownerPage.GetGrandTotalAsync()
                                .GetAwaiter().GetResult(),
                            Does.Contain("$0.00"));

                        Assert.That(
                            ownerPage.SubmitButton.IsDisabledAsync()
                                .GetAwaiter().GetResult(),
                            Is.True);
                    });
                }
                catch
                {
                    string artifacts = Path.Combine(
                        TestContext.CurrentContext.WorkDirectory,
                        "artifacts");

                    Directory.CreateDirectory(artifacts);

                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                artifacts,
                                "level3-remove-guest-failure.png"),
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
