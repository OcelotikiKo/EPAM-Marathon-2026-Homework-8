
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using AutoFixture.Xunit2;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace APIHandler.Tests.Services;

public class SubmitGroupPreOrderHandlerTests
{
    [Theory]
    [AutoData]
    public async Task Submit_ValidOwner_MergesAndSavesOrder(
        GroupReservation group,
        Reservation reservation,
        Order order)
    {
        reservation.CustomerId = "owner-123";
        group.ReservationId = reservation.Id;
        group.Status = GroupReservationStatus.Open;

        order.ReservationId = reservation.Id;
        
	order.Dishes =
	[
    	new()
    	{
        	DishId = "dish1",
        	Quantity = 4,
        	State = OrderItemState.Pending
    	},
    	new()
    	{
        	DishId = "owner-dish",
        	Quantity = 1,
        	State = OrderItemState.Pending
    	}
	];

        order.Status = OrderStatus.Created;

        var guestSessions = new List<GuestSession>
        {
            new()
            {
                Id = "guest1",
                GuestName = "Anna",
                GroupReservationId = group.GroupReservationId,
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
                GroupReservationId = group.GroupReservationId,
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
                GroupReservationId = group.GroupReservationId,
                IsSubmitted = false,
                Dishes =
                [
                    new() { DishId = "dish1", Quantity = 10 }
                ]
            }
        };

        var groupRepo = new Mock<IGroupReservationRepository>();
        var reservationRepo = new Mock<IReservationRepository>();
        var sessionRepo = new Mock<IGuestSessionRepository>();
        var orderRepo = new Mock<IOrderRepository>();

        groupRepo
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);

        reservationRepo
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        sessionRepo
            .Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync(guestSessions);

        orderRepo
            .Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);

        var handler = new SubmitGroupPreOrderHandler(
            groupRepo.Object,
            reservationRepo.Object,
            sessionRepo.Object,
            orderRepo.Object);

        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = group.GroupReservationId
            },
            Body = JsonSerializer.Serialize(new
            {
                reservationId = reservation.Id
            }),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = "owner-123"
                    }
                }
            }
        };

        var context = new Mock<ILambdaContext>();

        var response = await handler.HandleRequest(
            request, context.Object);

        Assert.Equal(200, response.StatusCode);

	Assert.Equal(3, order.Dishes.Count);
	Assert.Equal(9, order.Dishes.Single(d => d.DishId == "dish1").Quantity);
	Assert.Equal(1, order.Dishes.Single(d => d.DishId == "dish2").Quantity);
	Assert.Equal(1, order.Dishes.Single(d => d.DishId == "owner-dish").Quantity);


        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Submitted, group.Status);

        orderRepo.Verify(
            r => r.SaveOrderAsync(order), Times.Once);

        groupRepo.Verify(
            r => r.SaveGroupReservationAsync(group), Times.Once);
    }

	
    [Theory]
    [InlineAutoData("not-owner", 403)]
    [InlineAutoData("already-submitted", 400)]
    [InlineAutoData("no-sessions", 400)]
    public async Task Submit_InvalidRequest_ReturnsErrorWithoutSaving(
        string scenario,
        int expectedStatus,
        GroupReservation group,
        Reservation reservation,
        Order order)
    {
        group.ReservationId = reservation.Id;
        group.Status = scenario == "already-submitted"
            ? GroupReservationStatus.Submitted
            : GroupReservationStatus.Open;

        reservation.CustomerId = scenario == "not-owner"
            ? "another-owner"
            : "owner-123";

        var groupRepo = new Mock<IGroupReservationRepository>();
        var reservationRepo = new Mock<IReservationRepository>();
        var sessionRepo = new Mock<IGuestSessionRepository>();
        var orderRepo = new Mock<IOrderRepository>();

        groupRepo
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);

        reservationRepo
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        orderRepo
            .Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);

        sessionRepo
            .Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync(new List<GuestSession>());

        var handler = new SubmitGroupPreOrderHandler(
            groupRepo.Object,
            reservationRepo.Object,
            sessionRepo.Object,
            orderRepo.Object);

        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = group.GroupReservationId
            },
            Body = JsonSerializer.Serialize(new
            {
                reservationId = reservation.Id
            }),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        ["sub"] = "owner-123"
                    }
                }
            }
        };

        var logger = new Mock<ILambdaLogger>();
        var context = new Mock<ILambdaContext>();
        context.Setup(c => c.Logger).Returns(logger.Object);

        var response = await handler.HandleRequest(
            request, context.Object);

        Assert.Equal(expectedStatus, response.StatusCode);

        orderRepo.Verify(
            r => r.SaveOrderAsync(It.IsAny<Order>()),
            Times.Never);

        groupRepo.Verify(
            r => r.SaveGroupReservationAsync(
                It.IsAny<GroupReservation>()),
            Times.Never);
    }


}
