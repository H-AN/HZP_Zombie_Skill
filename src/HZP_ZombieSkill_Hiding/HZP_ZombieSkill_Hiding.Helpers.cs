using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using HanZombiePlagueS2;
using SwiftlyS2.Shared.SchemaDefinitions;



namespace HZP_ZombieSkill;

public class HZP_ZombieSkill_Hiding_Helpers
{
    private readonly ILogger<HZP_ZombieSkill_Hiding_Helpers> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Hiding_Globals _globals;

    public HZP_ZombieSkill_Hiding_Helpers(
        ISwiftlyCore core, 
        ILogger<HZP_ZombieSkill_Hiding_Helpers> logger,
        HZP_ZombieSkill_Hiding_Globals globals)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
    }

    public void GiveHidingSkill(IPlayer player, ZombieSkillGroup group)
    {
        if(player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if(pawn == null || !pawn.IsValid)
            return;

        var _zpApi = HZP_ZombieSkill_Hiding._zpApi;
        if (_zpApi == null)
            return;

        var playerId = player.PlayerID;
        
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new PlayerSkillState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        var zombieProperties = _zpApi.HZP_GetZombieProperties(group.Name);
        if (zombieProperties != null)
        {
            SetPlayerAlpha(pawn, group.Alpha);
            
            ApplyEffect(player, group.Duration);
            
            state.IsHidingActive = true;
            state.SkillEndTime = _core.Engine.GlobalVars.CurrentTime + group.Duration;
            state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime + group.Cooldown;

            int percentage = (int)((255 - group.Alpha) * 100 / 255);
            player.SendCenter(T(player, "HidingSkillActive", group.Duration, percentage));
            EmitSoundFormPlayer(player, group.SoundStart, 1.0f);

            _core.Scheduler.DelayBySeconds(group.Duration, () =>
            {
                CheckAndRestoreSkill(player, group);
            });

            CancelCooldownTimer(playerId);

            var cooldownSeconds = group.Cooldown;
            var cts = new CancellationTokenSource();
            _globals.SkillCdTimer[playerId] = cts;

            _core.Scheduler.DelayBySeconds(cooldownSeconds, () =>
            {
                if (cts.IsCancellationRequested) 
                    return;

                if (player == null || !player.IsValid) 
                    return;

                player.SendCenter(T(player, "HidingSkillReady"));

                _globals.SkillCdTimer.Remove(playerId);
            });
        }
    }

    public void CheckAndRestoreSkill(IPlayer player, ZombieSkillGroup _)
    {
        if(player == null || !player.IsValid)
            return;

        var playerId = player.PlayerID;
        
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        if (!state.IsHidingActive)
            return;

        if (_core.Engine.GlobalVars.CurrentTime >= state.SkillEndTime)
        {
            ResetPlayerSkill(player, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
        }
    }

    public void CancelCooldownTimer(int playerId)
    {
        if (_globals.SkillCdTimer.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimer.Remove(playerId);
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

        state.IsHidingActive = false;
        state.SkillEndTime = currentTime;
        state.LastButtonPressTime = 0f;

        if (resetCooldown)
        {
            state.CooldownEndTime = currentTime;
        }
    }

    public void ResetPlayerSkill(IPlayer player, bool resetCooldown = true, bool showEndMessage = false, bool cancelCooldownTimer = true)
    {
        if (player == null || !player.IsValid)
            return;

        var playerId = player.PlayerID;
        var hasState = _globals.PlayerSkillStates.TryGetValue(playerId, out var state);
        if (!hasState || state == null)
        {
            ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);
            return;
        }

        var wasActive = state.IsHidingActive;

        if (hasState)
        {
            var pawn = player.PlayerPawn;
            if (pawn != null && pawn.IsValid)
            {
                SetPlayerAlpha(pawn, 255);
                ResetProgressBar(pawn);
            }
        }

        ResetPlayerSkillState(playerId, resetCooldown, cancelCooldownTimer);

        if (showEndMessage && wasActive)
        {
            player.SendCenter(T(player, "HidingSkillEnded"));
        }
    }

    public void ApplyEffect(IPlayer player, float Time)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        SetProgressBar(pawn, Time);
    }

    public void SetProgressBar(CCSPlayerPawn pawn, float time)
    {
        if(pawn == null || !pawn.IsValid)
            return;

        ResetProgressBar(pawn);

        CSPlayerBlockingUseAction_t type = CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_None;

        int timeInt = (int)Math.Ceiling(time);
        pawn.ProgressBarDuration = timeInt;
        pawn.BlockingUseActionInProgress = type;
        pawn.ProgressBarStartTime = _core.Engine.GlobalVars.CurrentTime;
        
        pawn.ProgressBarDurationUpdated();
        pawn.BlockingUseActionInProgressUpdated();
        pawn.ProgressBarStartTimeUpdated();
    }

    public void ResetProgressBar(CCSPlayerPawn pawn)
    {
        if(pawn == null || !pawn.IsValid)
            return;

        pawn.ProgressBarDuration = 0;
        pawn.ProgressBarStartTime = 0f;
        pawn.ProgressBarDurationUpdated();
        pawn.ProgressBarStartTimeUpdated();
    }

    public void EmitSoundFormPlayer(IPlayer player, string SoundPath, float Volume)
    {
        if (string.IsNullOrWhiteSpace(SoundPath))
            return;

        var pwan = player.PlayerPawn;
        if (pwan == null || !pwan.IsValid)
            return;

        string[] sounds = SoundPath
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (sounds.Length == 0)
            return;

        string finalSound = sounds.Length == 1
            ? sounds[0]
            : sounds[Random.Shared.Next(sounds.Length)];

        var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(finalSound, Volume, 1.0f);
        sound.SourceEntityIndex = (int)pwan.Index;
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

    public void SetPlayerAlpha(CCSPlayerPawn pawn, int alpha)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.Render.A != (byte)alpha)
        {
            pawn.Render.A = (byte)alpha;
            pawn.RenderUpdated();
        }
    }
}
