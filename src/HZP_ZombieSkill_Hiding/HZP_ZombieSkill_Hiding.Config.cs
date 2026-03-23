namespace HZP_ZombieSkill;

public class ZombieSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; }
    public float Cooldown { get; set; }
    public int Alpha { get; set; }
    public string SoundStart { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public class HZP_ZombieSkill_Hiding_Config
{
    public List<ZombieSkillGroup> Groups { get; set; } = new List<ZombieSkillGroup>();
}
