
using System.Text.Json;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;
using Microsoft.Playwright;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
[Category("Diagnostics")]
public class OwnerRouteDiagnostics
{
    [Test]
    public async Task CheckOwnerRoute()
    {
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
                string authJson = JsonSerializer.Serialize(
                    new { idToken, accessToken });

                await context.AddInitScriptAsync(
                    script: "localStorage.setItem('auth', " +
                        JsonSerializer.Serialize(authJson) + ");");

                var page = await context.NewPageAsync();

                page.PageError += (_, error) =>
                    Console.WriteLine("JS error: " + error);

                string requestedUrl =
                    $"{TestSettings.FrontendUrl}/reservations/" +
                    $"{Guid.NewGuid()}/group-order";

                await page.GotoAsync(requestedUrl);

                await page.WaitForTimeoutAsync(3000);

                bool hasAuth = await page.EvaluateAsync<bool>(
                    "() => Boolean(localStorage.getItem('auth'))");

                var headings =
                    await page.Locator("h1").AllTextContentsAsync();

                string heading = headings.Count > 0
                    ? headings[0]
                    : "(no h1)";

                string body =
                    await page.Locator("body").InnerTextAsync();

                Console.WriteLine("Requested URL: " + requestedUrl);
                Console.WriteLine("Actual URL: " + page.Url);
                Console.WriteLine("Auth exists: " + hasAuth);
                Console.WriteLine("Page heading: " + heading);
                Console.WriteLine(
                    "Page text: " +
                    body[..Math.Min(body.Length, 300)]
                        .Replace("\r", " ")
                        .Replace("\n", " "));

                Assert.Multiple(() =>
                {
                    Assert.That(hasAuth, Is.True,
                        "Authentication was not preserved.");

                    Assert.That(page.Url,
                        Does.Contain("/group-order"),
                        "Owner View route redirected.");

                    Assert.That(heading,
                        Is.EqualTo("Group Pre-Order"),
                        "Expected Owner View was not rendered.");
                });
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
