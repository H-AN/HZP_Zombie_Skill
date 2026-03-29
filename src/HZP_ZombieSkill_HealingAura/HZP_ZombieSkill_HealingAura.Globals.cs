namespace HZP_ZombieSkill;

public sealed class HealingAuraPlayerState
{
    public float CooldownEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
}

public sealed class HealingAuraPayload
{
    public float HealAmount { get; init; }
    public float Radius { get; init; }
    public string SoundStart { get; init; } = string.Empty;

    // Beam光圈配置
    public int RingColorR { get; init; }
    public int RingColorG { get; init; }
    public int RingColorB { get; init; }
    public int RingColorA { get; init; }
    public float RingDuration { get; init; }
    public int RingSegments { get; init; }
    public float RingThickness { get; init; }

    // 被治疗者粒子
    public string TargetParticleEffect { get; init; } = string.Empty;
    public float TargetParticleLifetime { get; init; }
}

public sealed class HZP_ZombieSkill_HealingAura_Globals
{
    public Dictionary<int, HealingAuraPlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();
    public HashSet<int> ActiveBeamEntityIds { get; } = new();
    public HashSet<int> ActiveParticleEntityIds { get; } = new();
}
