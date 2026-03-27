namespace HZP_ZombieSkill;

public sealed class ShockwaveGrenadeSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public bool ReplaceGrenadeSlot { get; set; } = true;
    public bool AllowThrowerSelfAffect { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Cooldown { get; set; }
    public float ExplosionRadius { get; set; }
    public float ShakeAmplitude { get; set; }
    public float ShakeFrequency { get; set; }
    public float ShakeDuration { get; set; }
    public float KnockbackHorizontal { get; set; }
    public float KnockbackVertical { get; set; }
    public bool UseDistanceFalloff { get; set; }
    public float MinEffectScale { get; set; }
    public float SelfEffectRadius { get; set; }
    public float SelfShakeAmplitude { get; set; }
    public float SelfShakeFrequency { get; set; }
    public float SelfShakeDuration { get; set; }
    public float SelfKnockbackHorizontal { get; set; }
    public float SelfKnockbackVertical { get; set; }
    public bool SelfUseDistanceFalloff { get; set; }
    public float SelfMinEffectScale { get; set; }
    public int RingColorR { get; set; }
    public int RingColorG { get; set; }
    public int RingColorB { get; set; }
    public int RingColorA { get; set; }
    public float RingDuration { get; set; }
    public int RingSegments { get; set; }
    public float RingThickness { get; set; }
    public string TrailParticle { get; set; } = "particles/environment/de_train/train_coal_dump_trails.vpcf";
    public string ProjectileModel { get; set; } = string.Empty;
    public string WeaponCustomName { get; set; } = "zombie_shockwave_grenade";
    public string SoundGive { get; set; } = string.Empty;
    public string SoundExplode { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_ShockwaveGrenade_Config
{
    public List<ShockwaveGrenadeSkillGroup> Groups { get; set; } = new();
}
