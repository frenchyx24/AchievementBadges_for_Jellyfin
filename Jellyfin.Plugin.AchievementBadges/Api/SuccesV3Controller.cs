using Jellyfin.Plugin.AchievementBadges.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Jellyfin.Plugin.AchievementBadges.Api;

/// <summary>
/// 3.2.0 social endpoints for the Nflix app: weekly board, duels, unlock
/// reactions and progress alerts.
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/AchievementBadges")]
[ServiceFilter(typeof(UserOwnershipFilter))]
[ServiceFilter(typeof(AdminAuditLogFilter))]
[EnableRateLimiting("user-60-per-min")]
[ResponseCache(NoStore = true)]
public class SuccesV3Controller : ControllerBase
{
    private readonly SuccesV3Service _service;

    /// <summary>Initializes a new instance of the <see cref="SuccesV3Controller"/> class.</summary>
    public SuccesV3Controller(SuccesV3Service service)
    {
        _service = service;
    }

    /// <summary>Weekly XP board of the caller and the given ids.</summary>
    [HttpGet("users/{userId}/weekly-board")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetWeeklyBoard([FromRoute] string userId, [FromQuery] string? ids) => Ok(_service.GetWeeklyBoard(userId, ids));

    /// <summary>Duels of the caller.</summary>
    [HttpGet("users/{userId}/duels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetDuels([FromRoute] string userId) => Ok(_service.GetDuels(userId));

    /// <summary>Sends a duel.</summary>
    [HttpPost("users/{userId}/duels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult CreateDuel([FromRoute] string userId, [FromBody] NflixDuelCreateRequest request)
        => Answer(_service.CreateDuel(userId, request));

    /// <summary>Accepts a duel (opponent only).</summary>
    [HttpPost("users/{userId}/duels/{duelId}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult AcceptDuel([FromRoute] string userId, [FromRoute] string duelId)
        => Answer(_service.RespondDuel(userId, duelId, true));

    /// <summary>Declines a duel (opponent only).</summary>
    [HttpPost("users/{userId}/duels/{duelId}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult DeclineDuel([FromRoute] string userId, [FromRoute] string duelId)
        => Answer(_service.RespondDuel(userId, duelId, false));

    /// <summary>Recent unlocks of the caller and the given ids.</summary>
    [HttpGet("users/{userId}/unlock-feed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetUnlockFeed([FromRoute] string userId, [FromQuery] string? ids) => Ok(_service.GetUnlockFeed(userId, ids));

    /// <summary>Toggles a reaction on an unlock.</summary>
    [HttpPost("users/{userId}/unlock-feed/react")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult React([FromRoute] string userId, [FromBody] NflixReactRequest request)
        => Answer(_service.React(userId, request));

    /// <summary>Progress nudges.</summary>
    [HttpGet("users/{userId}/progress-alerts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetProgressAlerts([FromRoute] string userId) => Ok(_service.GetProgressAlerts(userId));

    private ActionResult Answer((bool Ok, string Message, object? Result) r)
        => r.Ok ? Ok(r.Result ?? new { r.Message }) : BadRequest(new { r.Message });
}
