namespace HZP_ZombieSkill;

public class PlayerSkillState
{
    public bool IsHidingActive { get; set; }
    public float CooldownEndTime { get; set; }
    public float SkillEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
}

public class HZP_ZombieSkill_Hiding_Globals
{
    public Dictionary<int, PlayerSkillState> PlayerSkillStates { get; set; } = new Dictionary<int, PlayerSkillState>();
    public Dictionary<int, CancellationTokenSource> SkillCdTimer = new Dictionary<int, CancellationTokenSource>();
}
