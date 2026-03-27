using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_Pounce_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Pounce_Globals _globals;

    public HZP_ZombieSkill_Pounce_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_Pounce_Globals globals,
        IOptionsMonitor<HZP_ZombieSkill_Pounce_Config> _)
    {
        _core = core;
        _globals = globals;
    }

    public PouncePlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new PouncePlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public bool IsPlayerOnGround(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return false;

        var pawn = player.PlayerPawn;
        return pawn != null && pawn.IsValid && pawn.GroundEntity.IsValid;
    }

    public void GivePounce(IPlayer player, PounceSkillGroup group)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var playerId = player.PlayerID;
        var state = GetOrCreatePlayerState(playerId);
        var payload = BuildPayload(group);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        state.IsActive = true;
        state.ActivePayload = payload;
        state.LastActivatedPayload = payload;
        state.SkillEndTime = currentTime + payload.Duration;
        state.CooldownEndTime = currentTime + payload.Cooldown;
        state.ActivationVersion = _globals.AllocateActivationVersion();

        var activationVersion = state.ActivationVersion;
        var launchImpulse = BuildLaunchImpulse(pawn, payload);
        var launchVelocity = pawn.AbsVelocity + launchImpulse;

        SetProgressBar(pawn, payload.Duration);

        // Keep origin unchanged to reduce clipping/stuck risk in narrow spaces.
        pawn.Teleport(null, null, launchVelocity);

        player.SendCenter(T(player, "PounceActive", payload.Duration));
        EmitSoundFromPlayer(player, payload.SoundStart, 1.0f);

        _core.Scheduler.DelayBySeconds(payload.Duration, () =>
        {
            TryRestoreAfterDuration(playerId, activationVersion);
        });

        CancelCooldownTimer(playerId);

        var timer = new CancellationTokenSource();
        _globals.SkillCdTimers[playerId] = timer;

        _core.Scheduler.DelayBySeconds(payload.Cooldown, () =>
        {
            if (timer.IsCancellationRequested)
                return;

            var nextPlayer = _core.PlayerManager.GetPlayer(playerId);
            if (nextPlayer != null && nextPlayer.IsValid)
            {
                nextPlayer.SendCenter(T(nextPlayer, "PounceReady"));
            }

            _globals.SkillCdTimers.Remove(playerId);
        });
    }

    public void TryRestoreAfterDuration(int playerId, int activationVersion)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        if (state.ActivationVersion != activationVersion)
            return;

        if (!state.IsActive)
            return;

        if (_core.Engine.GlobalVars.CurrentTime < state.SkillEndTime)
            return;

        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player != null && player.IsValid)
        {
            ResetPlayerSkill(player, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
        }
        else
        {
            ResetPlayerSkillState(playerId, resetCooldown: false, cancelCooldownTimer: false);
        }
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<PounceSkillGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.Enable)
                continue;

            if (string.IsNullOrWhiteSpace(group.PrecacheSound))
                continue;

            @event.AddItem(group.PrecacheSound);
        }
    }

    public void CleanupAllState()
    {
        foreach (var playerId in _globals.PlayerSkillStates.Keys.ToList())
        {
            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid)
            {
                ResetPlayerSkill(player);
            }
            else
            {
                ResetPlayerSkillState(playerId);
            }
        }

        foreach (var timer in _globals.SkillCdTimers.Values)
        {
            timer.Cancel();
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

        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        state.IsActive = false;
        state.ActivePayload = null;
        state.SkillEndTime = currentTime;
        state.LastButtonPressTime = 0.0f;

        if (resetCooldown)
        {
            state.CooldownEndTime = currentTime;
        }
    }

    public void ResetPlayerSkill(
        IPlayer player,
        bool resetCooldown = true,
        bool showEndMessage = false,
        bool cancelCooldownTimer = true)
    {
        if (player == null || !player.IsValid)
            return;

        var playerId = player.PlayerID;
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state) || state == null)
        {
            ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);
            return;
        }

        var wasActive = state.IsActive;
        var endSound = state.ActivePayload?.SoundEnd ?? state.LastActivatedPayload?.SoundEnd ?? string.Empty;

        var pawn = player.PlayerPawn;
        if (pawn != null && pawn.IsValid)
        {
            ResetProgressBar(pawn);
        }

        ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);

        if (!showEndMessage || !wasActive)
            return;

        player.SendCenter(T(player, "PounceEnded"));
        EmitSoundFromPlayer(player, endSound, 1.0f);
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

        var sounds = soundPath
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    private PouncePayload BuildPayload(PounceSkillGroup group)
    {
        return new PouncePayload
        {
            DeathRefresh = group.DeathRefresh,
            RequireOnGround = group.RequireOnGround,
            Duration = Math.Max(0.1f, group.Duration),
            Cooldown = Math.Max(0.0f, group.Cooldown),
            ForwardForce = Math.Max(1.0f, group.ForwardForce),
            VerticalForce = Math.Max(0.0f, group.VerticalForce),
            LookUpBonusForce = Math.Max(0.0f, group.LookUpBonusForce),
            // Backward compatible: keep config key LiftOffset, but treat it as extra Z velocity.
            LiftOffset = Math.Clamp(group.LiftOffset, 0.0f, 512.0f),
            SoundStart = group.SoundStart ?? string.Empty,
            SoundEnd = group.SoundEnd ?? string.Empty
        };
    }

    private Vector BuildLaunchImpulse(CCSPlayerPawn pawn, PouncePayload payload)
    {
        var eyeAngles = pawn.EyeAngles;
        eyeAngles.ToDirectionVectors(out var forward, out _, out _);

        var planarAngle = new QAngle(0.0f, eyeAngles.Yaw, 0.0f);
        planarAngle.ToDirectionVectors(out var planarForward, out _, out _);

        forward.Normalize();
        planarForward.Normalize();

        // Keep the leap direction stable on the horizontal plane, then let looking up add extra lift.
        var verticalVelocity = payload.VerticalForce
            + MathF.Max(0.0f, forward.Z) * payload.LookUpBonusForce
            + payload.LiftOffset;

        return new Vector(
            planarForward.X * payload.ForwardForce,
            planarForward.Y * payload.ForwardForce,
            verticalVelocity);
    }
}
