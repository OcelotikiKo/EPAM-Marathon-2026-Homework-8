
using System.Net;
using GreenTasty.Aqa.Fixtures;
using NUnit.Framework;
using GreenTasty.Aqa.Api;

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


[Test]
public async Task RemovingLastGuest_ShouldKeepOwnerAndZeroTotal()
{
    await using var fixture =
        await GroupOrderFixture.CreateAsync();

    string guestId = await fixture.AddGuestAsync();

    using var before =
        await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
            fixture.GroupId);

    Assert.That(
        before.RootElement
            .GetProperty("Participants").GetArrayLength(),
        Is.EqualTo(2));

    // Remove the only guest.
    using var response =
        await fixture.OwnerApi.DeleteGroupGuestAsync(
            fixture.GroupId, guestId);

    Assert.That(
        response.StatusCode,
        Is.EqualTo(HttpStatusCode.OK));

    // Read the actual group state after deletion.
    using var after =
        await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
            fixture.GroupId);

    var participants = after.RootElement
        .GetProperty("Participants")
        .EnumerateArray()
        .ToArray();

    decimal total = after.RootElement
        .GetProperty("Total")
        .GetDecimal();

    Assert.Multiple(() =>
    {
        Assert.That(participants, Has.Length.EqualTo(1));

        Assert.That(
            participants[0].GetProperty("Type").GetString(),
            Is.EqualTo("Owner"));

        Assert.That(
            participants.Any(p =>
                p.GetProperty("Id").GetString() == guestId),
            Is.False);

        Assert.That(total, Is.EqualTo(0m));
    });
}


[Test]
public async Task AnonymousUser_CannotDeleteGuest()
{
    await using var fixture =
        await GroupOrderFixture.CreateAsync();

    string guestId = await fixture.AddGuestAsync();

    using var anonymousApi = new RestaurantApiClient();

    using var response =
        await anonymousApi.DeleteGroupGuestAsync(
            fixture.GroupId, guestId);

    Assert.That(
        response.StatusCode,
        Is.AnyOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden));

    using var snapshot =
        await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
            fixture.GroupId);

    var participants = snapshot.RootElement
        .GetProperty("Participants")
        .EnumerateArray()
        .ToArray();

    Assert.Multiple(() =>
    {
        Assert.That(participants, Has.Length.EqualTo(2));

        Assert.That(
            participants.Any(p =>
                p.GetProperty("Id").GetString() == guestId),
            Is.True);
    });
}


[Test]
public async Task GuestFromAnotherGroup_ShouldReturn400_AndPreserveBothGroups()
{
    await using var groupA = await GroupOrderFixture.CreateAsync();
    await using var groupB = await GroupOrderFixture.CreateAsync();

    Assert.That(groupA.GroupId, Is.Not.EqualTo(groupB.GroupId));

    string guestA = await groupA.AddGuestAsync();
    string guestB = await groupB.AddGuestAsync();

    // Try to delete a guest from another group.
    using var response =
        await groupB.OwnerApi.DeleteGroupGuestAsync(
            groupB.GroupId, guestA);

    Assert.That(
        response.StatusCode,
        Is.EqualTo(HttpStatusCode.BadRequest));

    using var snapshotA =
        await groupA.OwnerApi.GetGroupSessionsDocumentAsync(groupA.GroupId);

    using var snapshotB =
        await groupB.OwnerApi.GetGroupSessionsDocumentAsync(groupB.GroupId);

    var participantsA = snapshotA.RootElement
        .GetProperty("Participants")
        .EnumerateArray()
        .Select(p => p.GetProperty("Id").GetString())
        .ToArray();

    var participantsB = snapshotB.RootElement
        .GetProperty("Participants")
        .EnumerateArray()
        .Select(p => p.GetProperty("Id").GetString())
        .ToArray();

    Assert.Multiple(() =>
    {
        Assert.That(participantsA, Has.Length.EqualTo(2));
        Assert.That(participantsB, Has.Length.EqualTo(2));
        Assert.That(participantsA, Does.Contain(guestA));
        Assert.That(participantsB, Does.Contain(guestB));
        Assert.That(participantsB, Does.Not.Contain(guestA));
    });
}


[Test]
public async Task SubmittedGroup_CannotRemoveGuest()
{
    await using var fixture =
        await GroupOrderFixture.CreateAsync();

    // Create two guest sessions.
    var (guest1Id, guest1Token) =
        await fixture.AddGuestWithTokenAsync();

    string guest2Id = await fixture.AddGuestAsync();

    using var guestApi = new RestaurantApiClient(guest1Token);

    // Select a valid dish.
    using var menu = await guestApi.GetGuestMenuAsync(guest1Id);

    var dish = menu.RootElement
        .GetProperty("dishes")
        .EnumerateObject()
        .SelectMany(category => category.Value.EnumerateArray())
        .First(d =>
            d.GetProperty("selectable").GetBoolean() &&
            d.GetProperty("price").GetDecimal() > 0);

    string dishId = dish.GetProperty("id").GetString()!;

    // Create Owner order and submit Guest 1's cart.
    using var ownerOrder =
        await fixture.OwnerApi.CreatePreOrderAsync(
            fixture.ReservationId, dishId, 1);

    await guestApi.AddGuestDishAsync(guest1Id, dishId, 1);
    await guestApi.SubmitGuestCartAsync(guest1Id);

    // Submit consolidated group pre-order.
    using var submission =
        await fixture.OwnerApi.TrySubmitGroupAsync(
            fixture.GroupId,
            fixture.ReservationId);

    string submissionBody =
        await submission.Content.ReadAsStringAsync();

    Assert.That(
        submission.StatusCode,
        Is.EqualTo(HttpStatusCode.OK),
        submissionBody);

    // Attempt to remove Guest 2 after submission.
    using var deletion =
        await fixture.OwnerApi.DeleteGroupGuestAsync(
            fixture.GroupId, guest2Id);

    string deletionBody =
        await deletion.Content.ReadAsStringAsync();

    Assert.That(
        deletion.StatusCode,
        Is.EqualTo(HttpStatusCode.Forbidden),
        deletionBody);

    // Verify that both guests remain in the group.
    using var snapshot =
        await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
            fixture.GroupId);

    var participants = snapshot.RootElement
        .GetProperty("Participants")
        .EnumerateArray()
        .ToArray();

    using var status =
        await fixture.OwnerApi.GetGroupStatusAsync(
            fixture.GroupId);

    Assert.Multiple(() =>
    {
        Assert.That(participants, Has.Length.EqualTo(3));

        Assert.That(
            participants.Any(p =>
                p.GetProperty("Id").GetString() == guest1Id),
            Is.True);

        Assert.That(
            participants.Any(p =>
                p.GetProperty("Id").GetString() == guest2Id),
            Is.True);

        Assert.That(
            status.RootElement
                .GetProperty("status").GetString(),
            Is.EqualTo("Submitted"));
    });
}


}
