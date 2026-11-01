
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Helpers;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace APIHandler.Tests.Services;

public class GroupPreOrderConsolidatorTests
{
    [Fact]
    public void Consolidate_MergesDishesAndSkipsUnsubmittedSessions()
    {
        var sessions = new List<GuestSession>
        {
            new()
            {
                Id = "guest1",
                GuestName = "Anna",
                IsSubmitted = true,
                Dishes =
                [
                    new() { DishId = "dish1", Quantity = 2 },
                    new() { DishId = "dish2", Quantity = 1 }
                ]
            },
            new()
            {
                Id = "guest2",
                GuestName = "Ivan",
                IsSubmitted = true,
                Dishes =
                [
                    new() { DishId = "dish1", Quantity = 3 }
                ]
            },
            new()
            {
                Id = "guest3",
                GuestName = "Olena",
                IsSubmitted = false,
                Dishes =
                [
                    new() { DishId = "dish1", Quantity = 10 }
                ]
            }
        };

        var result = GroupPreOrderConsolidator.Consolidate(sessions);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result.Single(d => d.DishId == "dish1").Quantity);
        Assert.Equal(1, result.Single(d => d.DishId == "dish2").Quantity);
        Assert.All(result, d => Assert.Equal(OrderItemState.Pending, d.State));
    }
}
