
using System.Net;
using GreenTasty.Aqa.Api;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
[Category("Smoke")]
public class ApiSmokeTests
{
    [Test]
    public async Task Locations_ShouldReturn200()
    {
        using var api = new RestaurantApiClient();

        using var response = await api.GetLocationsAsync();

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            "The locations endpoint should be accessible.");
    }

    [Test]
    public async Task GroupSessions_WithoutToken_ShouldBeRejected()
    {
        using var api = new RestaurantApiClient();

        var groupId = Guid.NewGuid().ToString();

        using var response =
            await api.GetGroupSessionsAsync(groupId);

        Assert.That(
            response.StatusCode,
            Is.AnyOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden),
            "Anonymous users must not access group sessions.");
    }
}
