
using System.Net;
using GreenTasty.Aqa.Api;
using GreenTasty.Aqa.Config;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
[Category("Level1")]
[Category("Authentication")]
public class OwnerAuthorizationTests
{
    [Test]
    public async Task Owner_ShouldSignInSuccessfully()
    {
        using var api = new RestaurantApiClient();

        string token = await api.SignInAsync(
            TestSettings.OwnerEmail,
            TestSettings.OwnerPassword);

        Assert.That(token, Is.Not.Empty);
        Assert.That(token.Split('.').Length, Is.EqualTo(3));
    }

    [Test]
    public async Task Owner_NonexistentGroup_ShouldReturn404()
    {
        using var authApi = new RestaurantApiClient();

        string token = await authApi.SignInAsync(
            TestSettings.OwnerEmail,
            TestSettings.OwnerPassword);

        using var ownerApi = new RestaurantApiClient(token);

        string nonexistentGroupId = Guid.NewGuid().ToString();

        using var response =
            await ownerApi.GetGroupSessionsAsync(
                nonexistentGroupId);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }
}
