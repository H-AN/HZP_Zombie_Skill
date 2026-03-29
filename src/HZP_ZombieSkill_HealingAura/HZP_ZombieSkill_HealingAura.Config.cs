namespace HZP_ZombieSkill;

public sealed class HealingAuraSkillGroup
{
    public bool Enable { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool DeathRefresh { get; set; }
    public float Cooldown { get; set; } = 30.0f;
    public float HealAmount { get; set; } = 500.0f;
    public float Radius { get; set; } = 300.0f;
    public string SoundStart { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;

    // Beam光圈颜色配置 (RGB 0-255)
    public int RingColorR { get; set; } = 0;
    public int RingColorG { get; set; } = 255;
    public int RingColorB { get; set; } = 0;
    public int RingColorA { get; set; } = 255;
    public float RingDuration { get; set; } = 1.0f;
    public int RingSegments { get; set; } = 32;
    public float RingThickness { get; set; } = 3.0f;

    // 被治疗者粒子效果 (可选)
    public string TargetParticleEffect { get; set; } = string.Empty;
    public float TargetParticleLifetime { get; set; } = 1.0f;
}

public sealed class HZP_ZombieSkill_HealingAura_Config
{
    public List<HealingAuraSkillGroup> Groups { get; set; } = new();
}
