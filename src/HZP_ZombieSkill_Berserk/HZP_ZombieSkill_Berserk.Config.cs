namespace HZP_ZombieSkill;

public class ZombieSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public float Cooldown { get; set; }
    public float SpeedMultiplier { get; set; }
    public int Fov { get; set; }
    public string SoundStart { get; set; } = string.Empty;
    public string SoundIdle { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public class HZP_ZombieSkill_Berserk_Config
{
    public List<ZombieSkillGroup> Groups { get; set; } = new List<ZombieSkillGroup>();
}
