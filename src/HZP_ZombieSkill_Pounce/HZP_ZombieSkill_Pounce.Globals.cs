namespace HZP_ZombieSkill;

public sealed class PouncePayload
{
    public bool DeathRefresh { get; init; }
    public bool RequireOnGround { get; init; }
    public float Duration { get; init; }
    public float Cooldown { get; init; }
    public float ForwardForce { get; init; }
    public float VerticalForce { get; init; }
    public float LookUpBonusForce { get; init; }
    public float LiftOffset { get; init; }
    public string SoundStart { get; init; } = string.Empty;
    public string SoundEnd { get; init; } = string.Empty;
}

public sealed class PouncePlayerState
{
    public bool IsActive { get; set; }
    public float CooldownEndTime { get; set; }
    public float SkillEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
    public int ActivationVersion { get; set; }
    public PouncePayload? ActivePayload { get; set; }
    public PouncePayload? LastActivatedPayload { get; set; }
}

public sealed class HZP_ZombieSkill_Pounce_Globals
{
    private int _nextActivationVersion = 1;

    public Dictionary<int, PouncePlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();

    public int AllocateActivationVersion()
    {
        return _nextActivationVersion++;
    }
}
