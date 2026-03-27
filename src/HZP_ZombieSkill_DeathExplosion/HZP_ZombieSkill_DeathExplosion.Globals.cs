namespace HZP_ZombieSkill;

public sealed class DeathExplosionPayload
{
    public float Damage { get; init; }
    public float Radius { get; init; }
    public bool UseDistanceFalloff { get; init; }
    public float MinimumDamageScale { get; init; }
    public string ExplosionParticle { get; init; } = string.Empty;
    public float ParticleLifetime { get; init; }
    public string SoundExplode { get; init; } = string.Empty;
}

public sealed class DeathExplosionPlayerState
{
    public float LastButtonPressTime { get; set; }
}

public sealed class HZP_ZombieSkill_DeathExplosion_Globals
{
    public Dictionary<int, DeathExplosionPlayerState> PlayerSkillStates { get; } = new();
    public HashSet<int> ActiveParticleEntityIds { get; } = new();
}
