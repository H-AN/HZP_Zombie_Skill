using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_DamageReduction_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_DamageReduction_Globals _globals;

    public HZP_ZombieSkill_DamageReduction_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_DamageReduction_Globals globals,
        IOptionsMonitor<HZP_ZombieSkill_DamageReduction_Config> _)
    {
        _core = core;
        _globals = globals;
    }

    public DamageReductionPlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new DamageReductionPlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public void GiveDamageReduction(IPlayer player, DamageReductionSkillGroup group)
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
        state.ActivationVersion++;

        var activationVersion = state.ActivationVersion;
        var reductionPercent = (1.0f - payload.DamageTakenScale) * 100.0f;

        SetProgressBar(pawn, payload.Duration);

        player.SendCenter(T(player, "DamageReductionActive", payload.Duration, reductionPercent));
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
                nextPlayer.SendCenter(T(nextPlayer, "DamageReductionReady"));
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

    public void TryHandleDamageReduction(IOnEntityTakeDamageEvent @event)
    {
        var zpApi = HZP_ZombieSkill_DamageReduction.ZpApi;
        if (zpApi == null)
            return;

        var victimPlayer = TryGetPlayerFromEntity(@event.Entity);
        if (victimPlayer == null || !victimPlayer.IsValid || !victimPlayer.IsAlive)
            return;

        var playerId = victimPlayer.PlayerID;
        if (!zpApi.HZP_IsZombie(playerId))
            return;

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state) || !state.IsActive || state.ActivePayload == null)
            return;

        if (zpApi.HZP_PlayerHaveGodState(playerId))
            return;

        var attackerPlayer = TryGetAttackerPlayer(@event.Info.Attacker.Value);
        if (attackerPlayer == null || !attackerPlayer.IsValid || !attackerPlayer.IsAlive)
            return;

        if (zpApi.HZP_IsZombie(attackerPlayer.PlayerID))
            return;

        if (@event.Info.Damage <= 0.0f)
            return;

        var damageTakenScale = Math.Clamp(state.ActivePayload.DamageTakenScale, 0.0f, 1.0f);
        if (damageTakenScale >= 0.999f)
            return;

        @event.Info.Damage *= damageTakenScale;
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<DamageReductionSkillGroup> groups)
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

        player.SendCenter(T(player, "DamageReductionEnded"));
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

    private DamageReductionPayload BuildPayload(DamageReductionSkillGroup group)
    {
        return new DamageReductionPayload
        {
            DeathRefresh = group.DeathRefresh,
            Duration = Math.Max(0.1f, group.Duration),
            Cooldown = Math.Max(0.0f, group.Cooldown),
            DamageTakenScale = Math.Clamp(group.DamageTakenScale, 0.0f, 1.0f),
            SoundStart = group.SoundStart ?? string.Empty,
            SoundEnd = group.SoundEnd ?? string.Empty
        };
    }

    private IPlayer? TryGetPlayerFromEntity(CEntityInstance entity)
    {
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return null;

        var pawn = entity.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid)
            return null;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return null;

        var player = _core.PlayerManager.GetPlayerFromPawn(pawn);
        return player != null && player.IsValid
            ? player
            : null;
    }

    private IPlayer? TryGetAttackerPlayer(CEntityInstance? entity)
    {
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return null;

        var pawn = entity.As<CCSPlayerPawn>();
        if (pawn == null || !pawn.IsValid)
            return null;

        var controller = pawn.Controller.Value?.As<CCSPlayerController>();
        if (controller == null || !controller.IsValid)
            return null;

        var player = _core.PlayerManager.GetPlayerFromController(controller);
        return player != null && player.IsValid
            ? player
            : null;
    }
}
