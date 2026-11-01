
using System.Text.Json;
using GreenTasty.Aqa.Fixtures;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level1;

[TestFixture]
[Category("Level1")]
public class GroupOrderTests
{
    [Test]
    public async Task Owner_ShouldSeeJoinedGuestWithoutDishes()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        using var initial =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var initialParticipants = initial.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        Assert.That(
            initialParticipants.Any(p =>
                p.GetProperty("Type").GetString() == "Owner"),
            Is.True,
            "Owner must be present in the group.");

        string guestId = await fixture.AddGuestAsync();

        using var updated =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var participants = updated.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        var guest = participants.Single(p =>
            p.GetProperty("Id").GetString() == guestId);

        Assert.Multiple(() =>
        {
            Assert.That(
                guest.GetProperty("Type").GetString(),
                Is.EqualTo("Guest"));

            Assert.That(
                guest.GetProperty("IsSubmitted").GetBoolean(),
                Is.False);

            Assert.That(
                guest.GetProperty("Items")
                    .GetArrayLength(),
                Is.Zero);

            Assert.That(
                guest.GetProperty("Total")
                    .GetDecimal(),
                Is.Zero);

            Assert.That(
                updated.RootElement.GetProperty("Total")
                    .GetDecimal(),
                Is.Zero);
        });
    }
}
