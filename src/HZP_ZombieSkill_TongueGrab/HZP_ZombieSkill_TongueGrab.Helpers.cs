using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_TongueGrab_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_TongueGrab_Globals _globals;

    public HZP_ZombieSkill_TongueGrab_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_TongueGrab_Globals globals)
    {
        _core = core;
        _globals = globals;
    }

    public TongueGrabPlayerState GetOrCreatePlayerState(int playerId, ulong currentSessionId = 0)
    {
        if (_globals.PlayerSkillStates.TryGetValue(playerId, out var existing))
        {
            if (currentSessionId != 0
                && existing.CasterSessionId != 0
                && existing.CasterSessionId != currentSessionId)
            {
                ResetPlayerSkillState(playerId);
                _globals.PlayerSkillStates.Remove(playerId);
            }
            else
            {
                return existing;
            }
        }

        var state = new TongueGrabPlayerState
        {
            CasterSessionId = currentSessionId
        };
        _globals.PlayerSkillStates[playerId] = state;
        return state;
    }

    public bool TryFindTarget(IPlayer caster, TongueGrabSkillGroup group, out IPlayer? target)
    {
        target = null;

        if (!TryCreateAimContext(caster, group, out var payload, out var casterPawn, out var beamStart, out var forward, out var solidTrace))
            return false;

        if (solidTrace.DidHit
            && solidTrace.HitPlayer(out IPlayer? directHit)
            && IsValidHumanTarget(caster, directHit, out var directTargetPawn)
            && HasLineOfSightToTarget(casterPawn, directTargetPawn, payload))
        {
            target = directHit;
            return true;
        }

        var playerTrace = new CGameTrace();
        _core.Trace.SimpleTrace(
            beamStart,
            beamStart + forward * payload.MaxGrabDistance,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Hitbox | MaskTrace.Player,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.Always,
            ref playerTrace,
            casterPawn);

        if (playerTrace.DidHit
            && playerTrace.HitPlayer(out IPlayer? hitPlayer)
            && IsValidHumanTarget(caster, hitPlayer, out var hitTargetPawn)
            && HasLineOfSightToTarget(casterPawn, hitTargetPawn, payload))
        {
            target = hitPlayer;
            return true;
        }

        var bestScore = float.MinValue;
        foreach (var candidate in _core.PlayerManager.GetAlive())
        {
            if (!IsValidHumanTarget(caster, candidate, out var candidatePawn))
                continue;

            var candidatePoint = ResolveBeamEnd(candidatePawn, payload);
            var delta = candidatePoint - beamStart;
            var distance = GetVectorLength(delta);
            if (distance <= 1.0f || distance > payload.MaxGrabDistance)
                continue;

            var direction = new Vector(
                delta.X / distance,
                delta.Y / distance,
                delta.Z / distance);
            var dot = Dot(forward, direction);
            if (dot < payload.TargetLockMinDot)
                continue;

            var projection = Dot(delta, forward);
            if (projection <= 0.0f || projection > payload.MaxGrabDistance)
                continue;

            var perpendicularDistance = GetDistanceFromRay(delta, forward, projection);
            if (perpendicularDistance > payload.TargetLockMaxDistanceFromRay)
                continue;

            if (!HasLineOfSightToTarget(casterPawn, candidatePawn, payload))
                continue;

            var score = dot * 100000.0f - perpendicularDistance * 1000.0f - distance;
            if (score <= bestScore)
                continue;

            bestScore = score;
            target = candidate;
        }

        return target != null;
    }

    public void ShowMissTongueBeam(IPlayer caster, TongueGrabSkillGroup group)
    {
        if (!TryCreateAimContext(caster, group, out var payload, out _, out var beamStart, out var forward, out var solidTrace))
            return;

        var beamEnd = ResolveMissBeamEnd(beamStart, forward, solidTrace, payload.MaxGrabDistance);
        CreateTemporaryBeam(beamStart, beamEnd, payload, payload.MissBeamDuration);
    }

    public void GiveTongueGrab(IPlayer caster, IPlayer target, TongueGrabSkillGroup group)
    {
        if (caster == null || !caster.IsValid || !caster.IsAlive)
            return;

        if (target == null || !target.IsValid || !target.IsAlive)
            return;

        var casterPawn = caster.PlayerPawn;
        var targetPawn = target.PlayerPawn;
        if (casterPawn == null || !casterPawn.IsValid || targetPawn == null || !targetPawn.IsValid)
            return;

        var playerId = caster.PlayerID;
        var state = GetOrCreatePlayerState(playerId, caster.SessionId);
        var payload = BuildPayload(group);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        ReleaseRuntimeObjects(state, stopTargetVelocity: true);

        state.IsActive = true;
        state.TargetPlayerId = target.PlayerID;
        state.CasterSessionId = caster.SessionId;
        state.TargetSessionId = target.SessionId;
        state.ActivePayload = payload;
        state.LastUsedPayload = payload;
        state.SkillEndTime = currentTime + payload.Duration;
        state.CooldownEndTime = currentTime + payload.Cooldown;
        state.ActivationVersion = _globals.AllocateActivationVersion();

        state.HasOriginalMovementState = false;
        if (payload.ImmobilizeCasterWhileActive)
        {
            CaptureMovementSnapshot(casterPawn, state);
            ApplyZombieImmobilization(casterPawn, state);
        }

        SetProgressBar(casterPawn, payload.Duration);

        state.BeamHandleRaw = CreateBeam(casterPawn, targetPawn, payload);

        caster.SendCenter(T(caster, "TongueGrabActive", payload.Duration, target.Name));
        EmitSoundFromPlayer(caster, payload.SoundStart, 1.0f);

        var activationVersion = state.ActivationVersion;
        _core.Scheduler.DelayBySeconds(payload.Duration, () =>
        {
            TryRestoreAfterDuration(playerId, activationVersion);
        });

        ScheduleCooldownReadyNotice(playerId, payload.Cooldown);
    }

    public void StartMissCooldown(IPlayer caster, TongueGrabSkillGroup group)
    {
        if (caster == null || !caster.IsValid)
            return;

        var playerId = caster.PlayerID;
        var state = GetOrCreatePlayerState(playerId, caster.SessionId);
        var payload = BuildPayload(group);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        ReleaseRuntimeObjects(state, stopTargetVelocity: true);

        state.IsActive = false;
        state.TargetPlayerId = 0;
        state.CasterSessionId = caster.SessionId;
        state.TargetSessionId = 0;
        state.ActivePayload = null;
        state.LastUsedPayload = payload;
        state.SkillEndTime = currentTime;
        state.CooldownEndTime = currentTime + payload.Cooldown;
        state.ActivationVersion = _globals.AllocateActivationVersion();

        caster.SendCenter(T(caster, "TongueGrabMissed", payload.Cooldown));
        EmitSoundFromPlayer(caster, payload.SoundMiss, 1.0f);

        ScheduleCooldownReadyNotice(playerId, payload.Cooldown);
    }

    public void TryRestoreAfterDuration(int playerId, int activationVersion)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        if (!state.IsActive || state.ActivationVersion != activationVersion)
            return;

        if (_core.Engine.GlobalVars.CurrentTime < state.SkillEndTime)
            return;

        if (TryResolvePlayerBySession(playerId, state.CasterSessionId, out var player) && player != null)
        {
            ResetPlayerSkill(player, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
        }
        else
        {
            ResetPlayerSkillState(playerId, resetCooldown: false, cancelCooldownTimer: false);
        }
    }

    public void UpdateActiveTongueGrabs()
    {
        var zpApi = HZP_ZombieSkill_TongueGrab.ZpApi;
        if (zpApi == null)
            return;

        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        foreach (var playerId in _globals.PlayerSkillStates.Keys.ToList())
        {
            if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
                continue;

            if (!state.IsActive || state.ActivePayload == null)
                continue;

            if (!TryResolvePlayerBySession(playerId, state.CasterSessionId, out var caster) || caster == null)
            {
                CancelCooldownTimer(playerId);
                _globals.PlayerSkillStates.Remove(playerId);
                continue;
            }

            var casterPawn = caster.PlayerPawn;
            if (casterPawn == null
                || !casterPawn.IsValid
                || casterPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE
                || !zpApi.HZP_IsZombie(caster.PlayerID))
            {
                ResetPlayerSkill(caster, resetCooldown: false, cancelCooldownTimer: false);
                continue;
            }

            if (currentTime >= state.SkillEndTime)
            {
                ResetPlayerSkill(caster, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            if (!TryResolvePlayerBySession(state.TargetPlayerId, state.TargetSessionId, out var target) || target == null)
            {
                ResetPlayerSkill(caster, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            var targetPawn = target.PlayerPawn;
            if (targetPawn == null
                || !targetPawn.IsValid
                || targetPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE
                || zpApi.HZP_IsZombie(target.PlayerID))
            {
                ResetPlayerSkill(caster, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            if (state.ActivePayload.ImmobilizeCasterWhileActive)
            {
                ApplyZombieImmobilization(casterPawn, state);
            }

            if (state.ActivePayload.EndSkillWhenTargetClose
                && GetDistanceToCasterAnchor(targetPawn, casterPawn, state.ActivePayload) <= state.ActivePayload.EndSkillWhenTargetCloseDistance)
            {
                ResetPlayerSkill(caster, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            ApplyPull(targetPawn, casterPawn, state.ActivePayload);

            if (state.ActivePayload.EndSkillWhenTargetClose
                && GetDistanceToCasterAnchor(targetPawn, casterPawn, state.ActivePayload) <= state.ActivePayload.EndSkillWhenTargetCloseDistance)
            {
                ResetPlayerSkill(caster, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            EnsureBeam(state, casterPawn, targetPawn, state.ActivePayload);
        }
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<TongueGrabSkillGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.Enable)
                continue;

            AddResourceList(@event, group.PrecacheSound);
            AddResourceList(@event, group.SoundStart);
            AddResourceList(@event, group.SoundEnd);
            AddResourceList(@event, group.SoundMiss);
        }
    }

    public void CleanupAllState()
    {
        foreach (var playerId in _globals.PlayerSkillStates.Keys.ToList())
        {
            if (_globals.PlayerSkillStates.TryGetValue(playerId, out var state)
                && TryResolvePlayerBySession(playerId, state.CasterSessionId, out var player)
                && player != null)
            {
                ResetPlayerSkill(player);
            }
            else
            {
                ResetPlayerSkillState(playerId);
            }
        }

        foreach (var token in _globals.SkillCdTimers.Values.ToList())
        {
            token.Cancel();
        }

        _globals.SkillCdTimers.Clear();
        _globals.PlayerSkillStates.Clear();
    }

    public void CancelCooldownTimer(int playerId)
    {
        if (_globals.SkillCdTimers.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimers.Remove(playerId);
        }
    }

    public void ResetPlayerSkillState(int playerId, bool resetCooldown = true, bool cancelCooldownTimer = true)
    {
        if (cancelCooldownTimer)
        {
            CancelCooldownTimer(playerId);
        }

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        ReleaseRuntimeObjects(state, stopTargetVelocity: true);

        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        state.IsActive = false;
        state.TargetPlayerId = 0;
        state.TargetSessionId = 0;
        state.ActivePayload = null;
        state.BeamHandleRaw = 0;
        state.SkillEndTime = currentTime;
        state.LastButtonPressTime = 0.0f;
        state.HasOriginalMovementState = false;

        if (resetCooldown)
        {
            state.CooldownEndTime = currentTime;
            state.LastUsedPayload = null;
        }
    }

    public void ResetPlayerSkill(
        IPlayer caster,
        bool resetCooldown = true,
        bool showEndMessage = false,
        bool cancelCooldownTimer = true)
    {
        if (caster == null || !caster.IsValid)
            return;

        var playerId = caster.PlayerID;
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);
            return;
        }

        var wasActive = state.IsActive;
        var endSound = state.ActivePayload?.SoundEnd ?? state.LastUsedPayload?.SoundEnd ?? string.Empty;

        var pawn = caster.PlayerPawn;
        if (pawn != null && pawn.IsValid)
        {
            RestoreZombieMovement(pawn, state);
            ResetProgressBar(pawn);
        }

        ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);

        if (!showEndMessage || !wasActive)
            return;

        caster.SendCenter(T(caster, "TongueGrabEnded"));
        EmitSoundFromPlayer(caster, endSound, 1.0f);
    }

    public void SetProgressBar(CCSPlayerPawn pawn, float duration)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        ResetProgressBar(pawn);

        var timeInt = (int)Math.Ceiling(duration);
        pawn.ProgressBarDuration = timeInt;
        pawn.BlockingUseActionInProgress = CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_None;
        pawn.ProgressBarStartTime = _core.Engine.GlobalVars.CurrentTime;

        pawn.ProgressBarDurationUpdated();
        pawn.BlockingUseActionInProgressUpdated();
        pawn.ProgressBarStartTimeUpdated();
    }

    public void ResetProgressBar(CCSPlayerPawn pawn)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        pawn.ProgressBarDuration = 0;
        pawn.ProgressBarStartTime = 0.0f;
        pawn.ProgressBarDurationUpdated();
        pawn.ProgressBarStartTimeUpdated();
    }

    public void EmitSoundFromPlayer(IPlayer player, string soundPath, float volume)
    {
        if (string.IsNullOrWhiteSpace(soundPath))
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var sounds = soundPath.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sounds.Length == 0)
            return;

        var finalSound = sounds.Length == 1
            ? sounds[0]
            : sounds[Random.Shared.Next(sounds.Length)];

        var sound = new SoundEvent(finalSound, volume, 1.0f)
        {
            SourceEntityIndex = (int)pawn.Index
        };
        sound.Recipients.AddAllPlayers();

        _core.Scheduler.NextTick(() =>
        {
            sound.Emit();
        });
    }

    public string T(IPlayer? player, string key, params object[] args)
    {
        if (player == null || !player.IsValid)
            return string.Format(key, args);

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        return localizer[key, args];
    }

    private TongueGrabPayload BuildPayload(TongueGrabSkillGroup group)
    {
        var clampedPullStopDistance = Math.Max(0.0f, group.PullStopDistance);

        return new TongueGrabPayload
        {
            DeathRefresh = group.DeathRefresh,
            Duration = Math.Max(0.1f, group.Duration),
            Cooldown = Math.Max(0.0f, group.Cooldown),
            MaxGrabDistance = Math.Max(32.0f, group.MaxGrabDistance),
            PullSpeed = Math.Max(1.0f, group.PullSpeed),
            PullStopDistance = clampedPullStopDistance,
            PullMinUpwardVelocity = MathF.Max(0.0f, group.PullMinUpwardVelocity),
            PullVerticalScale = Math.Clamp(group.PullVerticalScale, 0.0f, 1.0f),
            TargetLockMinDot = Math.Clamp(group.TargetLockMinDot, 0.0f, 0.9999f),
            TargetLockMaxDistanceFromRay = MathF.Max(1.0f, group.TargetLockMaxDistanceFromRay),
            EndSkillWhenTargetClose = group.EndSkillWhenTargetClose,
            EndSkillWhenTargetCloseDistance = MathF.Max(
                clampedPullStopDistance,
                MathF.Max(0.0f, group.EndSkillWhenTargetCloseDistance)),
            ImmobilizeCasterWhileActive = group.ImmobilizeCasterWhileActive,
            MissBeamDuration = Math.Max(0.02f, group.MissBeamDuration),
            PullAnchorZOffset = group.PullAnchorZOffset,
            BeamSourceZOffset = group.BeamSourceZOffset,
            BeamTargetZOffset = group.BeamTargetZOffset,
            BeamColorR = Math.Clamp(group.BeamColorR, 0, 255),
            BeamColorG = Math.Clamp(group.BeamColorG, 0, 255),
            BeamColorB = Math.Clamp(group.BeamColorB, 0, 255),
            BeamColorA = Math.Clamp(group.BeamColorA, 0, 255),
            BeamWidth = MathF.Max(0.5f, group.BeamWidth),
            BeamHaloScale = MathF.Max(0.0f, group.BeamHaloScale),
            StopTargetVelocityOnRelease = group.StopTargetVelocityOnRelease,
            SoundStart = group.SoundStart ?? string.Empty,
            SoundEnd = group.SoundEnd ?? string.Empty,
            SoundMiss = group.SoundMiss ?? string.Empty
        };
    }

    private bool TryCreateAimContext(
        IPlayer caster,
        TongueGrabSkillGroup group,
        out TongueGrabPayload payload,
        out CCSPlayerPawn casterPawn,
        out Vector beamStart,
        out Vector forward,
        out CGameTrace solidTrace)
    {
        payload = BuildPayload(group);
        solidTrace = new CGameTrace();
        beamStart = Vector.Zero;
        forward = Vector.Zero;
        casterPawn = null!;

        if (caster == null || !caster.IsValid || !caster.IsAlive)
            return false;

        casterPawn = caster.PlayerPawn!;
        if (casterPawn == null || !casterPawn.IsValid)
            return false;

        var eyePosition = casterPawn.EyePosition;
        if (eyePosition == null)
            return false;

        beamStart = new Vector(eyePosition.Value.X, eyePosition.Value.Y, eyePosition.Value.Z);
        casterPawn.EyeAngles.ToDirectionVectors(out forward, out _, out _);
        forward.Normalize();

        _core.Trace.SimpleTrace(
            beamStart,
            beamStart + forward * payload.MaxGrabDistance,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player | MaskTrace.Hitbox,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.Player,
            ref solidTrace,
            casterPawn);

        return true;
    }

    private bool IsValidHumanTarget(IPlayer caster, IPlayer? candidate, out CCSPlayerPawn targetPawn)
    {
        targetPawn = null!;

        if (candidate == null || !candidate.IsValid || !candidate.IsAlive)
            return false;

        if (candidate.PlayerID == caster.PlayerID)
            return false;

        var zpApi = HZP_ZombieSkill_TongueGrab.ZpApi;
        if (zpApi == null || zpApi.HZP_IsZombie(candidate.PlayerID))
            return false;

        targetPawn = candidate.PlayerPawn!;
        if (targetPawn == null || !targetPawn.IsValid)
            return false;

        return targetPawn.LifeState == (byte)LifeState_t.LIFE_ALIVE;
    }

    private bool HasLineOfSightToTarget(CCSPlayerPawn casterPawn, CCSPlayerPawn targetPawn, TongueGrabPayload payload)
    {
        var start = ResolveBeamStart(casterPawn, payload);
        var end = ResolveBeamEnd(targetPawn, payload);

        var trace = new CGameTrace();
        _core.Trace.SimpleTrace(
            start,
            end,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.Player,
            ref trace,
            casterPawn);

        return !trace.DidHit || trace.Fraction >= 1.0f;
    }

    private void CreateTemporaryBeam(Vector start, Vector end, TongueGrabPayload payload, float lifetime)
    {
        var beam = _core.EntitySystem.CreateEntity<CBeam>();
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return;

        beam.DispatchSpawn();
        beam.Render = new Color(payload.BeamColorR, payload.BeamColorG, payload.BeamColorB, payload.BeamColorA);
        beam.Width = payload.BeamWidth;
        beam.EndWidth = payload.BeamWidth;
        beam.HaloScale = payload.BeamHaloScale;
        beam.Teleport(start, QAngle.Zero, Vector.Zero);
        beam.EndPos = end;
        beam.EndPosUpdated();
        beam.AddEntityIOEvent("Kill", "", null!, null!, lifetime);
    }

    private static Vector ResolveMissBeamEnd(Vector beamStart, Vector forward, CGameTrace solidTrace, float maxDistance)
    {
        if (solidTrace.DidHit && solidTrace.Fraction < 1.0f)
            return solidTrace.EndPos;

        return beamStart + forward * maxDistance;
    }

    private static float Dot(Vector left, Vector right)
    {
        return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    }

    private static float GetVectorLength(Vector value)
    {
        return MathF.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
    }

    private static float GetDistanceFromRay(Vector delta, Vector forward, float projection)
    {
        var rayPoint = forward * projection;
        var perpendicular = delta - rayPoint;
        return GetVectorLength(perpendicular);
    }

    private static float GetDistanceToCasterAnchor(CCSPlayerPawn targetPawn, CCSPlayerPawn casterPawn, TongueGrabPayload payload)
    {
        var targetOrigin = targetPawn.AbsOrigin;
        var casterOrigin = casterPawn.AbsOrigin;
        if (targetOrigin == null || casterOrigin == null)
            return float.MaxValue;

        var anchor = new Vector(
            casterOrigin.Value.X,
            casterOrigin.Value.Y,
            casterOrigin.Value.Z + payload.PullAnchorZOffset);

        var delta = new Vector(
            anchor.X - targetOrigin.Value.X,
            anchor.Y - targetOrigin.Value.Y,
            (anchor.Z - targetOrigin.Value.Z) * payload.PullVerticalScale);

        return GetVectorLength(delta);
    }

    private void ScheduleCooldownReadyNotice(int playerId, float delaySeconds)
    {
        CancelCooldownTimer(playerId);

        var token = new CancellationTokenSource();
        _globals.SkillCdTimers[playerId] = token;

        var safeDelay = Math.Max(0.0f, delaySeconds);
        _core.Scheduler.DelayBySeconds(safeDelay, () =>
        {
            if (token.IsCancellationRequested)
                return;

            _globals.SkillCdTimers.Remove(playerId);
            HandleCooldownTimerElapsed(playerId);
        });
    }

    private void HandleCooldownTimerElapsed(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        var currentTime = _core.Engine.GlobalVars.CurrentTime;
        if (currentTime < state.CooldownEndTime)
        {
            ScheduleCooldownReadyNotice(playerId, state.CooldownEndTime - currentTime);
            return;
        }

        if (state.IsActive && currentTime < state.SkillEndTime)
        {
            ScheduleCooldownReadyNotice(playerId, state.SkillEndTime - currentTime);
            return;
        }

        if (TryResolvePlayerBySession(playerId, state.CasterSessionId, out var player) && player != null)
        {
            player.SendCenter(T(player, "TongueGrabReady"));
        }

        if (!state.IsActive && currentTime >= state.CooldownEndTime)
        {
            _globals.PlayerSkillStates.Remove(playerId);
        }
    }

    private bool TryResolvePlayerBySession(int playerId, ulong expectedSessionId, out IPlayer? player)
    {
        player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.IsValid)
        {
            player = null;
            return false;
        }

        if (expectedSessionId != 0 && player.SessionId != expectedSessionId)
        {
            player = null;
            return false;
        }

        return true;
    }

    private void CaptureMovementSnapshot(CCSPlayerPawn pawn, TongueGrabPlayerState state)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        state.OriginalMoveType = pawn.MoveType;
        state.OriginalActualMoveType = pawn.ActualMoveType;
        state.OriginalVelocityModifier = pawn.VelocityModifier;
        state.HasOriginalMovementState = true;
    }

    private void ApplyZombieImmobilization(CCSPlayerPawn pawn, TongueGrabPlayerState state)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        if (!state.HasOriginalMovementState)
        {
            CaptureMovementSnapshot(pawn, state);
        }

        if (pawn.MoveType != MoveType_t.MOVETYPE_NONE || pawn.ActualMoveType != MoveType_t.MOVETYPE_NONE)
        {
            pawn.MoveType = MoveType_t.MOVETYPE_NONE;
            pawn.ActualMoveType = MoveType_t.MOVETYPE_NONE;
            pawn.MoveTypeUpdated();
        }

        if (MathF.Abs(pawn.VelocityModifier) > 0.001f)
        {
            pawn.VelocityModifier = 0.0f;
            pawn.VelocityModifierUpdated();
        }

        pawn.Teleport(null, null, Vector.Zero);
    }

    private void RestoreZombieMovement(CCSPlayerPawn pawn, TongueGrabPlayerState state)
    {
        if (pawn == null || !pawn.IsValid || !state.HasOriginalMovementState)
            return;

        if (pawn.MoveType != state.OriginalMoveType || pawn.ActualMoveType != state.OriginalActualMoveType)
        {
            pawn.MoveType = state.OriginalMoveType;
            pawn.ActualMoveType = state.OriginalActualMoveType;
            pawn.MoveTypeUpdated();
        }

        if (MathF.Abs(pawn.VelocityModifier - state.OriginalVelocityModifier) > 0.001f)
        {
            pawn.VelocityModifier = state.OriginalVelocityModifier;
            pawn.VelocityModifierUpdated();
        }

        pawn.Teleport(null, null, Vector.Zero);
        state.HasOriginalMovementState = false;
    }

    private void ApplyPull(CCSPlayerPawn targetPawn, CCSPlayerPawn casterPawn, TongueGrabPayload payload)
    {
        var targetOrigin = targetPawn.AbsOrigin;
        var casterOrigin = casterPawn.AbsOrigin;
        if (targetOrigin == null || casterOrigin == null)
            return;

        var anchor = new Vector(
            casterOrigin.Value.X,
            casterOrigin.Value.Y,
            casterOrigin.Value.Z + payload.PullAnchorZOffset);

        var deltaX = anchor.X - targetOrigin.Value.X;
        var deltaY = anchor.Y - targetOrigin.Value.Y;
        var deltaZ = (anchor.Z - targetOrigin.Value.Z) * payload.PullVerticalScale;
        var horizontalDistance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

        if (distance <= payload.PullStopDistance)
        {
            if (payload.StopTargetVelocityOnRelease)
            {
                targetPawn.Teleport(null, null, Vector.Zero);
            }

            return;
        }

        if (distance < 0.001f)
            return;

        var invDistance = 1.0f / distance;
        var pullVelocity = new Vector(
            deltaX * invDistance * payload.PullSpeed,
            deltaY * invDistance * payload.PullSpeed,
            deltaZ * invDistance * payload.PullSpeed);

        if (payload.PullMinUpwardVelocity > 0.0f
            && horizontalDistance > payload.PullStopDistance
            && targetOrigin.Value.Z <= anchor.Z + 8.0f)
        {
            pullVelocity.Z = MathF.Max(pullVelocity.Z, payload.PullMinUpwardVelocity);
        }

        targetPawn.Teleport(null, null, pullVelocity);
    }

    private void EnsureBeam(
        TongueGrabPlayerState state,
        CCSPlayerPawn casterPawn,
        CCSPlayerPawn targetPawn,
        TongueGrabPayload payload)
    {
        var start = ResolveBeamStart(casterPawn, payload);
        var end = ResolveBeamEnd(targetPawn, payload);

        if (state.BeamHandleRaw == 0)
        {
            state.BeamHandleRaw = CreateBeam(casterPawn, targetPawn, payload);
            return;
        }

        var beamHandle = new CHandle<CBeam>(state.BeamHandleRaw);
        if (!beamHandle.IsValid || beamHandle.Value == null || !beamHandle.Value.IsValid || !beamHandle.Value.IsValidEntity)
        {
            state.BeamHandleRaw = CreateBeam(casterPawn, targetPawn, payload);
            return;
        }

        TeleportBeam(beamHandle.Value, start, end);
    }

    private uint CreateBeam(CCSPlayerPawn casterPawn, CCSPlayerPawn targetPawn, TongueGrabPayload payload)
    {
        var start = ResolveBeamStart(casterPawn, payload);
        var end = ResolveBeamEnd(targetPawn, payload);

        var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return 0;

        beam.Render = new Color(payload.BeamColorR, payload.BeamColorG, payload.BeamColorB, payload.BeamColorA);
        beam.Width = payload.BeamWidth;
        beam.EndWidth = payload.BeamWidth;
        beam.HaloScale = payload.BeamHaloScale;
        beam.Teleport(start, new QAngle(), Vector.Zero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.DispatchSpawn();

        var handle = _core.EntitySystem.GetRefEHandle(beam);
        return handle.IsValid ? handle.Raw : 0;
    }

    private void KillBeam(uint beamHandleRaw)
    {
        if (beamHandleRaw == 0)
            return;

        var beamHandle = new CHandle<CBeam>(beamHandleRaw);
        if (!beamHandle.IsValid || beamHandle.Value == null || !beamHandle.Value.IsValid || !beamHandle.Value.IsValidEntity)
            return;

        beamHandle.Value.AcceptInput("Kill", 0);
    }

    private void ReleaseRuntimeObjects(TongueGrabPlayerState state, bool stopTargetVelocity)
    {
        if (stopTargetVelocity
            && state.TargetPlayerId > 0
            && TryResolvePlayerBySession(state.TargetPlayerId, state.TargetSessionId, out var target)
            && target != null)
        {
            var targetPawn = target.PlayerPawn;
            if (targetPawn != null && targetPawn.IsValid && state.ActivePayload?.StopTargetVelocityOnRelease == true)
            {
                targetPawn.Teleport(null, null, Vector.Zero);
            }
        }

        KillBeam(state.BeamHandleRaw);
        state.BeamHandleRaw = 0;
    }

    private static void TeleportBeam(CBeam beam, Vector start, Vector end)
    {
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return;

        beam.Teleport(start, new QAngle(), Vector.Zero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.EndPosUpdated();
    }

    private static Vector ResolveBeamStart(CCSPlayerPawn pawn, TongueGrabPayload payload)
    {
        var eyePosition = pawn.EyePosition;
        if (eyePosition != null)
        {
            return new Vector(
                eyePosition.Value.X,
                eyePosition.Value.Y,
                eyePosition.Value.Z + payload.BeamSourceZOffset);
        }

        var origin = pawn.AbsOrigin ?? Vector.Zero;
        return new Vector(origin.X, origin.Y, origin.Z + 64.0f + payload.BeamSourceZOffset);
    }

    private static Vector ResolveBeamEnd(CCSPlayerPawn pawn, TongueGrabPayload payload)
    {
        var origin = pawn.AbsOrigin ?? Vector.Zero;
        return new Vector(origin.X, origin.Y, origin.Z + payload.BeamTargetZOffset);
    }

    private static void AddResourceList(IOnPrecacheResourceEvent @event, string resources)
    {
        if (string.IsNullOrWhiteSpace(resources))
            return;

        foreach (var resource in resources.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            @event.AddItem(resource);
        }
    }
}
