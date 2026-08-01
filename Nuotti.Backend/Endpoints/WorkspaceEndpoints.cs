using Nuotti.Backend.Commands;
using Nuotti.Backend.Middleware;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using System.Net.Mail;

namespace Nuotti.Backend.Endpoints;

public sealed record EmailRequest(string Email);
public sealed record RedeemLinkRequest(string Token);
public sealed record CreateWorkspaceRequest(string Name);
public sealed record InviteMemberRequest(string Email);

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/auth/magic-links", async (EmailRequest request, IWorkspaceAccessStore store,
            IWebHostEnvironment environment, IMagicLinkDelivery delivery, CancellationToken ct) =>
        {
            if (!ValidEmail(request.Email)) return Invalid("email", "A valid email address is required.");
            var link = await store.IssueSignInAsync(request.Email, ct);
            // Local development exposes the token so the journey can run without an email provider.
            // Deployed environments never disclose credentials in the HTTP response.
            if (environment.IsDevelopment()) return Results.Ok(link);
            return await delivery.DeliverAsync(request.Email, link, MagicLinkPurpose.SignIn, ct)
                ? Results.Accepted() : DeliveryUnavailable();
        });

        app.MapPost("/v1/auth/magic-links/redeem", async (RedeemLinkRequest request, IWorkspaceAccessStore store, CancellationToken ct) =>
        {
            var redeemed = await store.RedeemAsync(request.Token, ct);
            return redeemed is null ? Results.NotFound() : Results.Ok(redeemed);
        });

        app.MapGet("/v1/workspaces", async (HttpContext http, IWorkspaceAccessStore store, CancellationToken ct) =>
        {
            var principal = await WorkspaceHttpAccess.AuthenticateAsync(http, store, ct);
            return principal is null ? Results.Unauthorized() : Results.Ok(await store.ListAsync(principal, ct));
        });

        app.MapPost("/v1/workspaces", async (
            HttpContext http, CreateWorkspaceRequest request, IWorkspaceAccessStore store,
            Nuotti.Backend.Governance.ProductionGovernance governance, CancellationToken ct) =>
        {
            var principal = await WorkspaceHttpAccess.AuthenticateAsync(http, store, ct);
            if (principal is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                return Invalid("name", "Workspace name must be between 1 and 120 characters.");
            var created = await store.CreateWorkspaceAsync(principal, request.Name, ct);
            governance.GrantLaunchEntitlements(created.WorkspaceId);
            return Results.Ok(created);
        });

        app.MapPost("/v1/workspaces/{workspaceId}/select", async (
            HttpContext http, string workspaceId, IWorkspaceAccessStore store, CancellationToken ct) =>
        {
            var principal = await WorkspaceHttpAccess.AuthenticateAsync(http, store, ct);
            if (principal is null) return Results.Unauthorized();
            var selected = await store.SelectAsync(principal, workspaceId, ct);
            return selected is null ? Results.NotFound() : Results.Ok(selected);
        });

        app.MapPost("/v1/workspaces/{workspaceId}/invitations", async (
            HttpContext http, string workspaceId, InviteMemberRequest request,
            IWorkspaceAccessStore store, IWebHostEnvironment environment, IMagicLinkDelivery delivery,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access?.Role != WorkspaceRole.Owner) return Results.NotFound();
            if (!ValidEmail(request.Email)) return Invalid("email", "A valid email address is required.");
            var link = await store.InviteAsync(selected.Principal, workspaceId, request.Email, ct);
            if (link is null) return Results.NotFound();
            if (environment.IsDevelopment()) return Results.Ok(link);
            return await delivery.DeliverAsync(request.Email, link, MagicLinkPurpose.Invitation, ct)
                ? Results.Accepted() : DeliveryUnavailable();
        });

        app.MapGet("/v1/workspaces/{workspaceId}/members", async (
            HttpContext http, string workspaceId, IWorkspaceAccessStore store, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access?.Role != WorkspaceRole.Owner) return Results.NotFound();
            return Results.Ok(await store.MembersAsync(selected.Principal, workspaceId, ct));
        });

        app.MapDelete("/v1/workspaces/{workspaceId}/members/{memberUserId}", async (
            HttpContext http, string workspaceId, string memberUserId,
            IWorkspaceAccessStore store, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access?.Role != WorkspaceRole.Owner) return Results.NotFound();
            return await store.RevokeAsync(selected.Principal, workspaceId, memberUserId, ct)
                ? Results.NoContent() : Results.NotFound();
        });

        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/create", async (
            HttpContext http, string workspaceId, string sessionCode,
            IWorkspaceAccessStore store, ISessionCommandProcessor processor,
            ISessionWorkspaceBinder sessions, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var command = new CreateSession(sessionCode)
            {
                SessionCode = sessionCode,
                IssuedByRole = Role.Performer,
                IssuedById = selected.Principal.UserId
            };
            var result = await processor.ApplyAsync(sessionCode,
                Actor.Verified(Role.Performer, selected.Principal.UserId), command,
                CorrelationIdMiddleware.GetCorrelationId(http), ct, workspaceId);
            if (result.Outcome is Nuotti.Contracts.V1.Protocol.Outcome.Applied
                or Nuotti.Contracts.V1.Protocol.Outcome.Duplicate)
                sessions.Bind(sessionCode, workspaceId);
            return result.ToHttpResult();
        });

        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/start", async (
            HttpContext http, string workspaceId, string sessionCode,
            IWorkspaceAccessStore store, ISessionCommandProcessor processor, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var command = new StartGame
            {
                SessionCode = sessionCode,
                IssuedByRole = Role.Performer,
                IssuedById = selected.Principal.UserId
            };
            return (await processor.ApplyAsync(sessionCode,
                Actor.Verified(Role.Performer, selected.Principal.UserId), command,
                CorrelationIdMiddleware.GetCorrelationId(http), ct, workspaceId)).ToHttpResult();
        });
    }

    static bool ValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320) return false;
        try { return new MailAddress(value).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    static IResult Invalid(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] });

    static IResult DeliveryUnavailable() => Results.Problem(
        title: "Magic-link delivery unavailable",
        detail: "The sign-in email could not be sent. Try again later.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
