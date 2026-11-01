
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GuestSessions;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class GetOwnerGuestSessionIdHandler(
    IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        if (!request.TryGetPathValue("reservationId", out string? reservationId))
        {
            return ResponseBuilder.CreateErrorResponse(
                400, "Missing reservationId.", context.Logger);
        }

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
        {
            return ResponseBuilder.CreateErrorResponse(
                401, "Unauthorized.", context.Logger);
        }

        try
        {

var ownerInfo = await guestSessionService
    .GetOwnerGuestSessionIdAsync(userId, reservationId);

return ResponseBuilder.CreateSuccessResponse(new
{
    guestSessionId = ownerInfo.GuestSessionId,
    groupReservationId = ownerInfo.GroupReservationId
});

        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                404, ex.Message, context.Logger);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                404, ex.Message, context.Logger);
        }
        catch (GuestSessionNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                403, ex.Message, context.Logger);
        }
        catch (Exception)
        {
            return ResponseBuilder.CreateErrorResponse(
                500, "Internal server error.", context.Logger);
        }
    }
}
