
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using System.Collections.Generic;
using System.Linq;

namespace APIHandler.Helpers;

public static class GroupPreOrderConsolidator
{
    public static List<OrderItem> Consolidate(
        IEnumerable<GuestSession> sessions)
    {
        return sessions
            .Where(s => s.IsSubmitted)
            .SelectMany(s => s.Dishes)
            .GroupBy(d => d.DishId)
            .Select(g => new OrderItem
            {
                DishId = g.Key,
                Quantity = g.Sum(d => d.Quantity),
                State = OrderItemState.Pending
            })
            .ToList();
    }
}
