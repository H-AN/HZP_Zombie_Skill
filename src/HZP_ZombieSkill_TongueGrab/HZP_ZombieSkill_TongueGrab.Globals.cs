using SwiftlyS2.Shared.SchemaDefinitions;

namespace HZP_ZombieSkill;

public sealed class TongueGrabPayload
{
    public bool DeathRefresh { get; init; }
    public float Duration { get; init; }
    public float Cooldown { get; init; }
    public float MaxGrabDistance { get; init; }
    public float PullSpeed { get; init; }
    public float PullStopDistance { get; init; }
    public float PullMinUpwardVelocity { get; init; }
    public float PullVerticalScale { get; init; }
    public float TargetLockMinDot { get; init; }
    public float TargetLockMaxDistanceFromRay { get; init; }
    public bool EndSkillWhenTargetClose { get; init; }
    public float EndSkillWhenTargetCloseDistance { get; init; }
    public bool ImmobilizeCasterWhileActive { get; init; }
    public float MissBeamDuration { get; init; }
    public float PullAnchorZOffset { get; init; }
    public float BeamSourceZOffset { get; init; }
    public float BeamTargetZOffset { get; init; }
    public int BeamColorR { get; init; }
    public int BeamColorG { get; init; }
    public int BeamColorB { get; init; }
    public int BeamColorA { get; init; }
    public float BeamWidth { get; init; }
    public float BeamHaloScale { get; init; }
    public bool StopTargetVelocityOnRelease { get; init; }
    public string SoundStart { get; init; } = string.Empty;
    public string SoundEnd { get; init; } = string.Empty;
    public string SoundMiss { get; init; } = string.Empty;
}

public sealed class TongueGrabPlayerState
{
    public bool IsActive { get; set; }
    public int TargetPlayerId { get; set; }
    public ulong CasterSessionId { get; set; }
    public ulong TargetSessionId { get; set; }
    public float CooldownEndTime { get; set; }
    public float SkillEndTime { get; set; }
    public float LastButtonPressTime { get; set; }
    public int ActivationVersion { get; set; }
    public uint BeamHandleRaw { get; set; }
    public bool HasOriginalMovementState { get; set; }
    public MoveType_t OriginalMoveType { get; set; }
    public MoveType_t OriginalActualMoveType { get; set; }
    public float OriginalVelocityModifier { get; set; }
    public TongueGrabPayload? ActivePayload { get; set; }
    public TongueGrabPayload? LastUsedPayload { get; set; }
}

public sealed class HZP_ZombieSkill_TongueGrab_Globals
{
    private int _nextActivationVersion = 1;

    public Dictionary<int, TongueGrabPlayerState> PlayerSkillStates { get; } = new();
    public Dictionary<int, CancellationTokenSource> SkillCdTimers { get; } = new();

    public int AllocateActivationVersion()
    {
        return _nextActivationVersion++;
    }
}
