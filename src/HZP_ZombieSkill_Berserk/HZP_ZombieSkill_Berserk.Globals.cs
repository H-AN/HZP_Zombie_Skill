namespace HZP_ZombieSkill;

public class PlayerSkillState
{
    public bool IsBerserkActive { get; set; }
    public float CooldownEndTime { get; set; }
    public float SkillEndTime { get; set; }
    public int OriginalFov { get; set; }
    public float LastButtonPressTime { get; set; }

    public bool IsIdleSoundRunning;
}

public class HZP_ZombieSkill_Berserk_Globals
{
    public Dictionary<int, PlayerSkillState> PlayerSkillStates { get; set; } = new Dictionary<int, PlayerSkillState>();
}
