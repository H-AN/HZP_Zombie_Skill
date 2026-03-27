namespace HZP_ZombieSkill;

public sealed class ShockwaveGrenadePayload
{
    public bool DeathRefresh { get; init; }
    public bool ReplaceGrenadeSlot { get; init; }
    public bool AllowThrowerSelfAffect { get; init; }
    public float Cooldown { get; init; }
    public float ExplosionRadius { get; init; }
    public float ShakeAmplitude { get; init; }
    public float ShakeFrequency { get; init; }
    public float ShakeDuration { get; init; }
    public float KnockbackHorizontal { get; init; }
    public float KnockbackVertical { get; init; }
    public bool UseDistanceFalloff { get; init; }
    public float MinEffectScale { get; init; }
    public float SelfEffectRadius { get; init; }
    public float SelfShakeAmplitude { get; init; }
    public float SelfShakeFrequency { get; init; }
    public float SelfShakeDuration { get; init; }
    public float SelfKnockbackHorizontal { get; init; }
    public float SelfKnockbackVertical { get; init; }
    public bool SelfUseDistanceFalloff { get; init; }
    public float SelfMinEffectScale { get; init; }
    public int RingColorR { get; init; }
    public int RingColorG { get; init; }
    public int RingColorB { get; init; }
    public int RingColorA { get; init; }
    public float RingDuration { get; init; }
    public int RingSegments { get; init; }
    public float RingThickness { get; init; }
    public string TrailParticle { get; init; } = string.Empty;
    public string ProjectileModel { get; init; } = string.Empty;
    public string WeaponCustomName { get; init; } = string.Empty;
    public string SoundGive { get; init; } = string.Empty;
    public string SoundExplode { get; init; } = string.Empty;
}

public sealed class ShockwaveGrenadePlayerState
{
    public ShockwaveGrenadePayload? PendingGrenade { get; set; }
    public ShockwaveGrenadePayload? LastUsedPayload { get; set; }
    public float CooldownEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
}

public sealed class ActiveShockwaveProjectileState
{
    public int ThrowerPlayerId { get; init; }
    public ShockwaveGrenadePayload Payload { get; init; } = new();
}

public sealed class HZP_ZombieSkill_ShockwaveGrenade_Globals
{
    public Dictionary<int, ShockwaveGrenadePlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();
    public Dictionary<int, ActiveShockwaveProjectileState> ActiveProjectiles { get; } = new();
    public Dictionary<int, int> ProjectileTrailParticles { get; } = new();
}
