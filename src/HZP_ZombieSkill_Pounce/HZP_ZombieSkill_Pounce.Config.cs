namespace HZP_ZombieSkill;

public sealed class PounceSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public bool RequireOnGround { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public float Cooldown { get; set; }
    public float ForwardForce { get; set; } = 1100.0f;
    public float VerticalForce { get; set; } = 260.0f;
    public float LookUpBonusForce { get; set; } = 180.0f;
    public float LiftOffset { get; set; } = 6.0f;
    public string SoundStart { get; set; } = string.Empty;
    public string SoundEnd { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_Pounce_Config
{
    public List<PounceSkillGroup> Groups { get; set; } = new();
}
