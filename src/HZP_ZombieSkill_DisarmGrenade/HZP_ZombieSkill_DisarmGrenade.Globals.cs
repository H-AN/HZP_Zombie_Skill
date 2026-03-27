namespace HZP_ZombieSkill;

public sealed class DisarmGrenadePayload
{
    public bool DeathRefresh { get; init; }
    public bool ReplaceGrenadeSlot { get; init; }
    public float Cooldown { get; init; }
    public string WeaponCustomName { get; init; } = string.Empty;
    public string SoundGive { get; init; } = string.Empty;
    public string SoundHit { get; init; } = string.Empty;
}

public sealed class DisarmGrenadePlayerState
{
    public DisarmGrenadePayload? PendingGrenade { get; set; }
    public DisarmGrenadePayload? LastUsedPayload { get; set; }
    public float CooldownEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
}

public sealed class ActiveDisarmProjectileState
{
    public int ThrowerPlayerId { get; init; }
    public DisarmGrenadePayload Payload { get; init; } = new();
}

public sealed class HZP_ZombieSkill_DisarmGrenade_Globals
{
    public Dictionary<int, DisarmGrenadePlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();
    public Dictionary<int, ActiveDisarmProjectileState> ActiveProjectiles { get; } = new();
}
