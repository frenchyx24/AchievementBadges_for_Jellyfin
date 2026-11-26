using System;
using Jellyfin.Plugin.AchievementBadges.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Jellyfin.Plugin.AchievementBadges.Api;

/// <summary>
/// "Succès v2" lot 3 routes. Same prefix, auth, ownership filter, rate limit
/// and no-store policy as <see cref="AchievementBadgesController"/>.
/// GET/POST users/{userId}/cosmetics already belong to the shop, so profile
/// frames and titles live under users/{userId}/profile-cosmetics.
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/AchievementBadges")]
[ServiceFilter(typeof(UserOwnershipFilter))]
[ServiceFilter(typeof(AdminAuditLogFilter))]
[EnableRateLimiting("user-60-per-min")]
[ResponseCache(NoStore = true)]
public class SuccesV2Controller : ControllerBase
{
    private readonly SuccesV2Service _service;

    /// <summary>Initializes a new instance of the <see cref="SuccesV2Controller"/> class.</summary>
    public SuccesV2Controller(SuccesV2Service service)
    {
        _service = service;
    }

    /// <summary>Progressive hints for hidden badges still locked.</summary>
    [HttpGet("users/{userId}/secret-hints")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetSecretHints([FromRoute] string userId) => Ok(_service.GetSecretHints(userId));

    /// <summary>Thematic collections from Jellyfin BoxSets.</summary>
    [HttpGet("users/{userId}/collections")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetCollections([FromRoute] string userId) => Ok(_service.GetCollections(userId));

    /// <summary>Current season pass.</summary>
    [HttpGet("users/{userId}/season-pass")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetSeasonPass([FromRoute] string userId) => Ok(_service.GetSeasonPass(userId));

    /// <summary>Profile frames and titles.</summary>
    [HttpGet("users/{userId}/profile-cosmetics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetProfileCosmetics([FromRoute] string userId) => Ok(_service.GetCosmetics(userId));

    /// <summary>Equips a profile frame and/or title.</summary>
    [HttpPost("users/{userId}/profile-cosmetics/equip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult EquipProfileCosmetics([FromRoute] string userId, [FromBody] NflixProfileCosmeticEquipRequest request)
    {
        var (ok, message, result) = _service.Equip(userId, request);
        return ok ? Ok(result) : BadRequest(new { Message = message });
    }

    /// <summary>Equipped frame and title of another user.</summary>
    [HttpGet("profiles/{targetUserId}/cosmetics")]
    [EnableRateLimiting("ip-30-per-min")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetPublicProfileCosmetics([FromRoute] string targetUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId) || targetUserId.Length > 64 || !Guid.TryParse(targetUserId, out _))
            return NotFound();
        return Ok(_service.GetPublicEquipped(targetUserId));
    }

    /// <summary>Today's daily challenge.</summary>
    [HttpGet("users/{userId}/daily-challenge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetDailyChallenge([FromRoute] string userId)
    {
        var c = _service.GetDailyChallenge(userId);
        return c is null ? NotFound() : Ok(c);
    }
}
