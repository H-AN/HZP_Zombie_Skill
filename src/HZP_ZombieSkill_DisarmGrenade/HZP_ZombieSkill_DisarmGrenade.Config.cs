namespace HZP_ZombieSkill;

public sealed class DisarmGrenadeSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public bool ReplaceGrenadeSlot { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public float Cooldown { get; set; }
    public string WeaponCustomName { get; set; } = "zombie_disarm_grenade";
    public string SoundGive { get; set; } = string.Empty;
    public string SoundHit { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_DisarmGrenade_Config
{
    public List<DisarmGrenadeSkillGroup> Groups { get; set; } = new();
}
