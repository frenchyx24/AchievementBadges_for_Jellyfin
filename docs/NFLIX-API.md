# Nflix aggregate API

`GET /Plugins/AchievementBadges/users/{userId}/nflix`

One call that returns everything the Nflix clients show on a profile. Same
auth as `users/{userId}`: a signed-in Jellyfin user (`[Authorize]`), and the
`UserOwnershipFilter` only lets the user themself or an admin read `{userId}`.
Default rate limit `user-60-per-min`, `Cache-Control: no-store`.

Badge titles and descriptions are localized to French (`Language: "fr"`).

## Fields

| Field | Content |
|---|---|
| `SchemaVersion` | `1`. Bumped on a breaking change. |
| `Language` | `"fr"`. |
| `Badges` | Same list as `GET users/{userId}` (French). |
| `Summary` | Same object as `GET users/{userId}/summary`. |
| `Rank` | Same object as `GET users/{userId}/rank`. |
| `Equipped` | Same list as `GET users/{userId}/equipped` (French). |
| `RarityStats` | Same object as `GET badges/rarity-stats`. |
| `Level` | `{ Level, Xp, XpIntoLevel, XpForNextLevel, Title }`. |
| `Streak` | `{ Current, Best }` from `Summary.CurrentWatchStreak` / `Summary.BestWatchStreak`. |
| `Recent` | Same as `GET users/{userId}/recent-unlocks?limit=10`. |
| `Next` | Same as `GET users/{userId}/next-badges?limit=6`. |
| `Seasonal` | `{ Active, Name, EndsAt }`. |

### Level and XP

XP per unlocked badge, by `Rarity`: Common 10, Uncommon 25, Rare 50, Epic 100,
Legendary 250, Mythic 500 (unknown rarity counts as Common).

The cumulative XP to finish level `n` is `50 * n * (n + 1)`: level 1 starts at
0 XP, level 2 at 100, level 3 at 300, level 10 at 4500.

- `Xp`: total XP.
- `XpIntoLevel`: XP earned inside the current level.
- `XpForNextLevel`: size of the current level (`100 * Level`); progress is
  `XpIntoLevel / XpForNextLevel`.
- `Title`: 1–4 « Spectateur », 5–9 « Habitué », 10–19 « Cinéphile »,
  20–34 « Critique », 35–49 « Maître du binge », 50+ « Légende ».

### Seasonal

UTC, inclusive days: Halloween Oct 15 – Nov 2, Noël Dec 10 – Jan 2,
Été Jun 21 – Aug 31. `EndsAt` is ISO 8601, the first instant after the last
day. Outside a window: `{ "Active": false, "Name": null, "EndsAt": null }`.

## Example

Badge objects are cut to a few fields here; the real ones carry every field of
`users/{userId}`.

```json
{
  "SchemaVersion": 1,
  "Language": "fr",
  "Badges": [
    { "Id": "first-movie", "Title": "Premier film", "Rarity": "Common", "RarityLabel": "Commun", "Unlocked": true, "UnlockedAt": "2026-10-20T21:14:00Z", "CurrentValue": 1, "TargetValue": 1 }
  ],
  "Summary": { "Unlocked": 12, "Total": 120, "Percentage": 10, "EquippedCount": 2, "Score": 340, "CurrentWatchStreak": 3, "BestWatchStreak": 9 },
  "Rank": {
    "Score": 340,
    "Tier": { "Name": "Silver", "Label": "Argent", "MinScore": 250, "Color": "#c0c0c0", "Icon": "military_tech" },
    "NextTier": { "Name": "Gold", "Label": "Or", "MinScore": 600, "Color": "#ffd700", "Icon": "military_tech" },
    "ProgressToNext": 26,
    "Tiers": []
  },
  "Equipped": [],
  "RarityStats": { "first-movie": 87.5 },
  "Level": { "Level": 3, "Xp": 420, "XpIntoLevel": 120, "XpForNextLevel": 300, "Title": "Spectateur" },
  "Streak": { "Current": 3, "Best": 9 },
  "Recent": [],
  "Next": [],
  "Seasonal": { "Active": true, "Name": "Halloween", "EndsAt": "2026-11-03T00:00:00.0000000Z" }
}
```
