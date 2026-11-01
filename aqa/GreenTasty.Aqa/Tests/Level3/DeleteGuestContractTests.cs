
using System.Net;
using System.Text.Json;
using GreenTasty.Aqa.Fixtures;
using NUnit.Framework;

namespace GreenTasty.Aqa.Tests.Level3;

[TestFixture]
[Category("Level3")]
[Category("PostDeploy")]
public class DeleteGuestContractTests
{
    [Test]
    public async Task DeleteResponse_ShouldMatchUpdatedGroup()
    {
        await using var fixture =
            await GroupOrderFixture.CreateAsync();

        string removedId = await fixture.AddGuestAsync();
        string remainingId = await fixture.AddGuestAsync();

        using var response =
            await fixture.OwnerApi.DeleteGroupGuestAsync(
                fixture.GroupId, removedId);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        using var result = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var returnedParticipants = result.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        using var snapshot =
            await fixture.OwnerApi.GetGroupSessionsDocumentAsync(
                fixture.GroupId);

        var actualParticipants = snapshot.RootElement
            .GetProperty("Participants")
            .EnumerateArray()
            .ToArray();

        string[] returnedIds = returnedParticipants
            .Select(p => p.GetProperty("Id").GetString()!)
            .ToArray();

        string[] actualIds = actualParticipants
            .Select(p => p.GetProperty("Id").GetString()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(returnedParticipants.Length,
                Is.EqualTo(2));

            Assert.That(returnedParticipants.Any(p =>
                p.GetProperty("Type").GetString() == "Owner"),
                Is.True);

            Assert.That(returnedIds,
                Does.Contain(remainingId));

            Assert.That(returnedIds,
                Does.Not.Contain(removedId));

            Assert.That(returnedIds,
                Is.EquivalentTo(actualIds));

            Assert.That(
                result.RootElement.GetProperty("Total")
                    .GetDecimal(),
                Is.EqualTo(
                    snapshot.RootElement.GetProperty("Total")
                        .GetDecimal()));
        });
    }
}
