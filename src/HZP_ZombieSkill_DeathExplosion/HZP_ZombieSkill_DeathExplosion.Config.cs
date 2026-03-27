namespace HZP_ZombieSkill;

public sealed class DeathExplosionSkillGroup
{
    public bool Enable { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Damage { get; set; } = 250.0f;
    public float Radius { get; set; } = 260.0f;
    public bool UseDistanceFalloff { get; set; }
    public float MinimumDamageScale { get; set; } = 0.35f;
    public float HintCooldown { get; set; } = 1.0f;
    public string ExplosionParticle { get; set; } = "particles/explosions_fx/explosion_hegrenade_water_intial_trail.vpcf";
    public float ParticleLifetime { get; set; } = 1.0f;
    public string SoundExplode { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_DeathExplosion_Config
{
    public List<DeathExplosionSkillGroup> Groups { get; set; } = new();
}
