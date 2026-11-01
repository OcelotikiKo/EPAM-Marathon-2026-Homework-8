
using System.Net;
using GreenTasty.Aqa.Fixtures;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level3;

[TestFixture]
[Category("Level3")]
[Category("Negative")]
public class RemoveGuestNegativeTests
{
    [Test]
    public async Task OwnerSession_CannotBeDeleted()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        using var ownerInfo =
            await fixture.OwnerApi.GetOwnerSessionAsync(
                fixture.ReservationId);

        string ownerId = ownerInfo.RootElement
            .GetProperty("guestSessionId").GetString()!;

        using var response =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, ownerId);

        string body =
            await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                response.StatusCode,
                Is.EqualTo(HttpStatusCode.Forbidden));

            Assert.That(
                body,
                Does.Contain("Owner session cannot be deleted"));
        });

        using var snapshot =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        Assert.That(
            snapshot.RootElement
                .GetProperty("Participants").GetArrayLength(),
            Is.EqualTo(1));
    }

    [Test]
    public async Task NonexistentGuest_ShouldReturn404()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        string nonexistentId = Guid.NewGuid().ToString();

        using var response =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, nonexistentId);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeletingSameGuestTwice_ShouldReturn404()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        string guestId = await fixture.AddGuestAsync();

        using var first =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, guestId);

        Assert.That(
            first.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        using var second =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, guestId);

        Assert.That(
            second.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));

        using var snapshot =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        Assert.That(
            snapshot.RootElement
                .GetProperty("Participants").GetArrayLength(),
            Is.EqualTo(1));
    }
}
