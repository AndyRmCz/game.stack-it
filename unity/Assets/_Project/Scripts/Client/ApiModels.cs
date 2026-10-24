using System.Collections.Generic;

// DTOs of the backend v1 API (backend/app/api/schemas.py). Field names are PascalCase here and
// snake_case on the wire; the ApiClient's serializer converts. Nullable server fields use
// nullable types so "missing" and "zero" stay distinct.
namespace StackIt.Client.Api
{
    public sealed class ApiError
    {
        public string Code;
        public string Message;
    }

    public sealed class ErrorEnvelope
    {
        public ApiError Error;
    }

    public sealed class GuestAuthRequest
    {
        public string DeviceId;
        public string DeviceSecret;
    }

    public sealed class ProviderTokenRequest
    {
        public string IdToken;
        public string Nonce;
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken;
    }

    public sealed class TokenResponse
    {
        public string UserId;
        public string AccessToken;
        public string RefreshToken;
        public int ExpiresIn;
        public bool IsNewUser;
    }

    public sealed class Profile
    {
        public string UserId;
        public string DisplayName;
        public string FriendCode;
        public int BestScore;
        public int Coins;
        public string EquippedBlockSkin;
        public string EquippedTheme;
        public string EquippedMusic;
        public List<string> Providers = new List<string>();
    }

    public sealed class RenameRequest
    {
        public string DisplayName;
    }

    /// <summary>One of the day's three missions (GET /v1/missions).</summary>
    public sealed class Mission
    {
        public int Slot;
        public string Kind;
        public int Target;
        public int Progress;
        public int Reward;
        public bool Claimed;

        public bool Done => Progress >= Target;
    }

    public sealed class MissionChest
    {
        public int Reward;
        public bool Ready;
        public bool Claimed;
    }

    public sealed class MissionBoard
    {
        public const int ChestSlot = 3;

        public string Day;
        public List<Mission> Missions = new List<Mission>();
        public MissionChest Chest;
        public int? Balance;

        /// <summary>Something is waiting to be claimed (the Home dot).</summary>
        public bool Claimable => Missions.Exists(m => m.Done && !m.Claimed) || (Chest != null && Chest.Ready && !Chest.Claimed);
    }

    public sealed class ClaimMissionRequest
    {
        public int Slot;
    }

    public sealed class StartRunRequest
    {
        public string ChallengeCode;
        public bool Daily;
    }

    public sealed class Rival
    {
        public string UserId;
        public string DisplayName;
        public int Score;
        public int Placed;
    }

    public sealed class StartRunResponse
    {
        public string RunId;
        public long Seed;
        public int SimVersion;
        public bool ContinuesAllowed;
        public string ChallengeCode;
        public Rival Rival;
        public int? DailyNumber;
    }

    public sealed class RunEvent
    {
        public int T;
        public string Type;
    }

    public sealed class SubmitRunRequest
    {
        public List<RunEvent> Events = new List<RunEvent>();
        public int ClaimedScore;
    }

    public sealed class ChallengeOutcome
    {
        public string Code;
        public Rival Rival;
        public bool Won;
    }

    public sealed class SubmitRunResponse
    {
        public string RunId;
        public string Status;
        public string Reason;
        public int? Score;
        public int BestScore;
        public bool NewBest;
        public int CoinsEarned;
        public bool CoinsDoubled;
        public int Balance;
        public ChallengeOutcome Challenge;
    }

    public sealed class BalanceResponse
    {
        public int Balance;
    }

    public sealed class CreateChallengeRequest
    {
        public string RunId;
    }

    public sealed class ChallengeInfo
    {
        public string Code;
        public string Url;
        public string CreatorId;
        public string CreatorName;
        public int Score;
        public int Placed;
        public string ExpiresAt;
        public bool Expired;
        public bool IsOwn;
        public int? MyBest;
    }

    public sealed class Friend
    {
        public string UserId;
        public string DisplayName;
        public int? BestScore;
        public int? BestPlaced;
    }

    public sealed class AddFriendRequest
    {
        public string FriendCode;
    }

    public sealed class LeaderboardEntry
    {
        public int Rank;
        public string UserId;
        public string DisplayName;
        public int Score;
        public bool IsMe;
    }

    public sealed class Leaderboard
    {
        public string Period;
        public string Since;
        public List<LeaderboardEntry> Entries = new List<LeaderboardEntry>();
    }

    public sealed class ShopItem
    {
        public string SkinId;
        public string Kind;
        public int Price;
        public bool Owned;
        public bool Equipped;
    }

    public sealed class Shop
    {
        public int Balance;
        public List<ShopItem> Items = new List<ShopItem>();
    }

    public sealed class SkinRequest
    {
        public string SkinId;
    }

    public sealed class DailyStatus
    {
        public int Streak;
        public bool ClaimedToday;
        public string Today;
        public int NextReward;
        public int? ClaimedAmount;
        public int? Balance;
    }

    public sealed class TelemetryBatchResponse
    {
        public int Accepted;
        public int Rejected;
    }
}
