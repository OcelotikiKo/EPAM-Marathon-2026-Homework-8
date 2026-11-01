
using GreenTasty.Aqa.Config;
using Microsoft.Playwright;

namespace GreenTasty.Aqa.Pages;

public sealed class GroupOrderPage
{
    private readonly IPage _page;

    public GroupOrderPage(IPage page)
    {
        _page = page;
    }

    public async Task OpenAsync(string reservationId)
    {
        string url =
            $"{TestSettings.FrontendUrl}/reservations/" +
            $"{Uri.EscapeDataString(reservationId)}/group-order";

        var response = await _page.GotoAsync(
            url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

        if (response == null || !response.Ok)
            throw new InvalidOperationException(
                $"Failed to open Owner View: {url}");

        await _page.Locator("section.summary").WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30000
            });
    }

    public ILocator OwnerCard =>
        _page.Locator("app-participant-card")
            .Filter(new LocatorFilterOptions
            {
                HasText = "My Pre-Order"
            });

    public ILocator GuestCard(string name) =>
        _page.Locator("app-participant-card")
            .Filter(new LocatorFilterOptions
            {
                HasText = $"Guest: {name}"
            });

    public ILocator Participants =>
        _page.Locator("ul.participants > li");

    public ILocator Status =>
        _page.Locator(".status-badge");

    public ILocator SubmitButton =>
        _page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions
            {
                Name = "Submit Pre-Order",
                Exact = true
            });


public async Task SubmitAsync()
{
    await SubmitButton.ClickAsync();

    await _page.GetByText(
        "Confirm Pre-Order",
        new PageGetByTextOptions
        {
            Exact = true
        }).WaitForAsync();

    await _page.GetByRole(
        AriaRole.Button,
        new PageGetByRoleOptions
        {
            Name = "Submit",
            Exact = true
        }).ClickAsync();

    await _page.GetByText(
        "Sent to Restaurant",
        new PageGetByTextOptions
        {
            Exact = true
        }).WaitForAsync(
            new LocatorWaitForOptions
            {
                Timeout = 30000
            });
}


    public async Task<string> GetGrandTotalAsync()
    {
        return await _page
            .Locator("section.summary")
            .InnerTextAsync();
    }
}
