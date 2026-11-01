
using Microsoft.Playwright;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
public class AccessTests
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    private const string BaseUrl =
        "https://dl8v07zbkza4a.cloudfront.net";

    [OneTimeSetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });
    }

    [Test]
    public async Task MainPage_ShouldLoad()
    {
        var page = await _browser.NewPageAsync();

        try
        {
            var response = await page.GotoAsync(
                $"{BaseUrl}/main");

            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Ok, Is.True);

            Assert.That(
                await page.Locator("body").IsVisibleAsync(),
                Is.True);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Test]
    public async Task AnonymousUser_CannotAccessOwnerView()
    {
        var page = await _browser.NewPageAsync();

        try
        {
            await page.GotoAsync(
                $"{BaseUrl}/reservations/test-id/group-order");

            await page.WaitForURLAsync(
                url => !url.Contains("/group-order"),
                new PageWaitForURLOptions
                {
                    Timeout = 15000
                });

            Assert.That(
                page.Url,
                Does.Not.Contain("/group-order"));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        if (_browser != null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
    }
}
