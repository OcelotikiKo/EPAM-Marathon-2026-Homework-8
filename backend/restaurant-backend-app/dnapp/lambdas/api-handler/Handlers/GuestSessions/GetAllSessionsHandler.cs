
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GuestSessions;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class GetAllSessionsHandler(
    IGuestSessionService guestSessionsService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        if (!request.TryGetPathValue(
            "groupReservationId", out string? groupReservationId))
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

        try
        {
            var result = await guestSessionsService
                .GetAllSessionsAsync(userId, groupReservationId);

            return ResponseBuilder.CreateSuccessResponse(result);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                404, ex.Message, context.Logger);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(
                403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorCodeResponse(ex);
        }
        catch (Exception)
        {
            return ResponseBuilder.CreateErrorResponse(
                500, "Internal server error.", context.Logger);
        }
    }
}
