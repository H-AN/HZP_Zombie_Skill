namespace HZP_ZombieSkill;

public sealed class TongueGrabSkillGroup
{
    public bool Enable { get; set; }
    public bool DeathRefresh { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Duration { get; set; } = 3.5f;
    public float Cooldown { get; set; } = 18.0f;
    public float MaxGrabDistance { get; set; } = 1000.0f;
    public float PullSpeed { get; set; } = 700.0f;
    public float PullStopDistance { get; set; } = 72.0f;
    public float PullMinUpwardVelocity { get; set; } = 110.0f;
    public float PullVerticalScale { get; set; } = 0.35f;
    public float TargetLockMinDot { get; set; } = 0.985f;
    public float TargetLockMaxDistanceFromRay { get; set; } = 24.0f;
    public bool EndSkillWhenTargetClose { get; set; }
    public float EndSkillWhenTargetCloseDistance { get; set; } = 80.0f;
    public bool ImmobilizeCasterWhileActive { get; set; } = true;
    public float MissBeamDuration { get; set; } = 0.12f;
    public float PullAnchorZOffset { get; set; } = 0.0f;
    public float BeamSourceZOffset { get; set; } = 0.0f;
    public float BeamTargetZOffset { get; set; } = 38.0f;
    public int BeamColorR { get; set; } = 100;
    public int BeamColorG { get; set; } = 255;
    public int BeamColorB { get; set; } = 120;
    public int BeamColorA { get; set; } = 220;
    public float BeamWidth { get; set; } = 3.0f;
    public float BeamHaloScale { get; set; } = 3.0f;
    public bool StopTargetVelocityOnRelease { get; set; } = true;
    public string SoundStart { get; set; } = string.Empty;
    public string SoundEnd { get; set; } = string.Empty;
    public string SoundMiss { get; set; } = string.Empty;
    public string PrecacheSound { get; set; } = string.Empty;
}

public sealed class HZP_ZombieSkill_TongueGrab_Config
{
    public List<TongueGrabSkillGroup> Groups { get; set; } = new();
}
