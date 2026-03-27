namespace HZP_ZombieSkill;

public sealed class DamageReductionPayload
{
    public bool DeathRefresh { get; init; }
    public float Duration { get; init; }
    public float Cooldown { get; init; }
    public float DamageTakenScale { get; init; }
    public string SoundStart { get; init; } = string.Empty;
    public string SoundEnd { get; init; } = string.Empty;
}

public sealed class DamageReductionPlayerState
{
    public bool IsActive { get; set; }
    public float CooldownEndTime { get; set; }
    public float SkillEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
    public int ActivationVersion { get; set; }
    public DamageReductionPayload? ActivePayload { get; set; }
    public DamageReductionPayload? LastActivatedPayload { get; set; }
}

public sealed class HZP_ZombieSkill_DamageReduction_Globals
{
    public Dictionary<int, DamageReductionPlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();
}
