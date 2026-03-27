namespace HZP_ZombieSkill;

public sealed class DamageReductionSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public float Cooldown { get; set; }
    public float DamageTakenScale { get; set; } = 0.35f;
    public string SoundStart { get; set; } = string.Empty;
    public string SoundEnd { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_DamageReduction_Config
{
    public List<DamageReductionSkillGroup> Groups { get; set; } = new();
}
