
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class SubmitGroupPreOrderHandler(
    IGroupReservationRepository groupRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository sessionRepository,
    IOrderRepository orderRepository) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        if (!request.TryGetPathValue(
            "groupReservationId", out string? groupId))
        {
            return ResponseBuilder.CreateErrorResponse(
                400, "Missing groupReservationId.", context.Logger);
        }

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue(
                "sub", out var userId))
        {
            return ResponseBuilder.CreateErrorResponse(
                401, "Unauthorized.", context.Logger);
        }

        string? reservationId;

        try
        {
            using var json = JsonDocument.Parse(request.Body ?? "");

            if (!json.RootElement.TryGetProperty(
                "reservationId", out var property) ||
                property.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(property.GetString()))
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "Missing reservationId.", context.Logger);
            }

            reservationId = property.GetString();
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(
                400, "Invalid JSON body.", context.Logger);
        }

        try
        {
            var group = await groupRepository
                .GetGroupReservationByIdAsync(groupId);

            if (group == null)
            {
                return ResponseBuilder.CreateErrorResponse(
                    404, "Group reservation not found.", context.Logger);
            }

            if (group.ReservationId != reservationId)
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "Reservation does not belong to this group.",
                    context.Logger);
            }

            var reservation = await reservationRepository
                .GetReservationByIdAsync(reservationId!);

            if (reservation == null)
            {
                return ResponseBuilder.CreateErrorResponse(
                    404, "Reservation not found.", context.Logger);
            }

            if (!string.Equals(
                reservation.CustomerId, userId,
                StringComparison.OrdinalIgnoreCase))
            {
                return ResponseBuilder.CreateErrorResponse(
                    403, "Only the Owner can submit this order.",
                    context.Logger);
            }

            if (group.Status == GroupReservationStatus.Submitted ||
                group.Status == GroupReservationStatus.Closed)
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "Group reservation is already submitted or closed.",
                    context.Logger);
            }

            var order = await orderRepository
                .GetOrderByReservationIdAsync(reservationId!);

            if (order == null)
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "Order not found.", context.Logger);
            }

            var sessions = await sessionRepository
                .GetAllByGroupReservationIdAsync(groupId);


var submittedSessions = sessions
    .Where(s => s.IsSubmitted && !s.IsOwnerSession)
    .ToList();


            if (submittedSessions.Count == 0)
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "No submitted guest sessions.",
                    context.Logger);
            }

            var dishes = GroupPreOrderConsolidator
                .Consolidate(submittedSessions);

            if (dishes.Count == 0)
            {
                return ResponseBuilder.CreateErrorResponse(
                    400, "Submitted sessions contain no dishes.",
                    context.Logger);
            }

            order.Dishes ??= [];

            foreach (var dish in dishes)
            {
                var existingDish = order.Dishes.FirstOrDefault(
                    d => d.DishId == dish.DishId &&
                         d.State == dish.State);

                if (existingDish != null)
                {
                    existingDish.Quantity += dish.Quantity;
                }
                else
                {
                    order.Dishes.Add(dish);
                }
            }

            order.Status = OrderStatus.SubmittedByCustomer;

            await orderRepository.SaveOrderAsync(order);

            group.Status = GroupReservationStatus.Submitted;

            await groupRepository.SaveGroupReservationAsync(group);

            var result = dishes.Select(d => new
            {
                dishId = d.DishId,
                quantity = d.Quantity
            }).ToArray();

            return ResponseBuilder.CreateSuccessResponse(result);
        }
        catch (Exception ex)
        {
            context.Logger.LogLine(ex.ToString());

            return ResponseBuilder.CreateErrorResponse(
                500, "Internal server error.", context.Logger);
        }
    }
}
