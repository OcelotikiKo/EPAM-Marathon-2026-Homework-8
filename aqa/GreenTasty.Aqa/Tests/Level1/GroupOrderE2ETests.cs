
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
public class GroupOrderE2ETests
{
    [Test]
    public async Task Owner_ShouldSeeGuestInBrowser()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        string guestId = await fixture.AddGuestAsync();

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

        using var api = new RestaurantApiClient();

        var (idToken, accessToken) =
            await api.SignInTokensAsync(
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
                // Authenticate before Angular starts.
                string authJson = JsonSerializer.Serialize(
                    new { idToken, accessToken });

                string script =
                    "localStorage.setItem('auth', " +
                    JsonSerializer.Serialize(authJson) + ");";

                await context.AddInitScriptAsync(
                    script: script);

                var page = await context.NewPageAsync();
                var ownerPage = new GroupOrderPage(page);

                string artifactDirectory = Path.Combine(
                    TestContext.CurrentContext.WorkDirectory,
                    "artifacts");

                Directory.CreateDirectory(artifactDirectory);

                try
                {
                    await ownerPage.OpenAsync(
                        fixture.ReservationId);

                    string ownerText =
                        await ownerPage.OwnerCard.InnerTextAsync();

                    string guestText =
                        await ownerPage.GuestCard(guestName)
                            .InnerTextAsync();

                    string status =
                        await ownerPage.Status.InnerTextAsync();

                    string total =
                        await ownerPage.GetGrandTotalAsync();

                    int participantCount =
                        await ownerPage.Participants.CountAsync();

                    bool submitDisabled =
                        await ownerPage.SubmitButton.IsDisabledAsync();

                    Assert.Multiple(() =>
                    {
                        Assert.That(
                            ownerText,
                            Does.Contain("My Pre-Order"));

                        Assert.That(
                            guestText,
                            Does.Contain(guestName));

                        Assert.That(
                            guestText,
                            Does.Contain("Still choosing"));

                        Assert.That(
                            guestText,
                            Does.Contain("No dishes yet"));

                        Assert.That(
                            participantCount,
                            Is.EqualTo(2));

                        Assert.That(
                            status,
                            Does.Contain("Collecting pre-orders"));

                        Assert.That(
                            total,
                            Does.Contain("$0.00"));

                        Assert.That(
                            submitDisabled,
                            Is.True);
                    });
                }
                catch
                {
                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(
                                artifactDirectory,
                                "level1-owner-view-failure.png"),
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
