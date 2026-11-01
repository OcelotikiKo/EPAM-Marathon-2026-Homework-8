
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




Console.WriteLine(
    $"Group participants count: {initialParticipants.Length}");

foreach (var participant in initialParticipants)
{
    string id = participant.TryGetProperty("Id", out var idValue)
        ? idValue.ToString()
        : "<missing>";

    string type = participant.TryGetProperty("Type", out var typeValue)
        ? $"{typeValue} ({typeValue.ValueKind})"
        : "<missing>";

    Console.WriteLine($"Participant: Id={id}, Type={type}");
}

using var ownerInfo = await fixture.OwnerApi.GetOwnerSessionAsync(
    fixture.ReservationId);

Console.WriteLine(
    "Expected Owner session ID: " +
    ownerInfo.RootElement.GetProperty("guestSessionId").GetString());

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
