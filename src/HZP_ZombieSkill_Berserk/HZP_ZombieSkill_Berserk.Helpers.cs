using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using HanZombiePlagueS2;
using SwiftlyS2.Shared.SchemaDefinitions;



namespace HZP_ZombieSkill;

public class HZP_ZombieSkill_Berserk_Helpers
{
    private readonly ILogger<HZP_ZombieSkill_Berserk_Helpers> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Berserk_Globals _globals;

    public HZP_ZombieSkill_Berserk_Helpers(
        ISwiftlyCore core, 
        ILogger<HZP_ZombieSkill_Berserk_Helpers> logger,
        HZP_ZombieSkill_Berserk_Globals globals)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
    }

    public void GiveBerserkSkill(IPlayer player, ZombieSkillGroup group)
    {
        if(player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if(pawn == null || !pawn.IsValid)
            return;

        var _zpApi = HZP_ZombieSkill_Berserk._zpApi;
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
            if (player.Controller != null && player.Controller.IsValid)
            {
                state.OriginalFov = (int)player.Controller.DesiredFOV;
            }
            
            pawn.VelocityModifier = zombieProperties.Speed + group.SpeedMultiplier;
            pawn.VelocityModifierUpdated();
            
            ApplyEffect(player, group.Duration, group.Fov);
            
            state.IsBerserkActive = true;
            state.SkillEndTime = _core.Engine.GlobalVars.CurrentTime + group.Duration;
            state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime + group.Cooldown;

            player.SendCenter(T(player, "BerserkSkillActive", group.Duration, group.SpeedMultiplier));
            EmitSoundFormPlayer(player, group.SoundStart, 1.0f);
            if (!state.IsIdleSoundRunning)
            {
                IdleSound(player, group.SoundIdle);
            }

            _core.Scheduler.DelayBySeconds(group.Duration, () =>
            {
                CheckAndRestoreSkill(player, group);
            });

            if (_globals.SkillCdTimer.TryGetValue(playerId, out var oldTimer))
            {
                oldTimer.Cancel();
                _globals.SkillCdTimer.Remove(playerId);
            }

            var cooldownSeconds = group.Cooldown;
            var cts = new CancellationTokenSource();
            _globals.SkillCdTimer[playerId] = cts;

            _core.Scheduler.DelayBySeconds(cooldownSeconds, () =>
            {
                if (cts.IsCancellationRequested) 
                    return;

                if (player == null || !player.IsValid) 
                    return;

                player.SendCenter(T(player, "BerserkSkillReady"));

                // Timer 执行完后清理
                _globals.SkillCdTimer.Remove(playerId);
            });
        }
    }

    public void CheckAndRestoreSkill(IPlayer player, ZombieSkillGroup group)
    {
        if(player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if(pawn == null || !pawn.IsValid)
            return;

        var _zpApi = HZP_ZombieSkill_Berserk._zpApi;
        if (_zpApi == null)
            return;

        var playerId = player.PlayerID;
        
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        if (!state.IsBerserkActive)
            return;

        if (state.IsBerserkActive && _core.Engine.GlobalVars.CurrentTime >= state.SkillEndTime)
        {
            var zombieProperties = _zpApi.HZP_GetZombieProperties(group.Name);
            if (zombieProperties != null)
            {
                pawn.VelocityModifier = zombieProperties.Speed;
                pawn.VelocityModifierUpdated();
                
                if (player.Controller != null && player.Controller.IsValid)
                {
                    player.Controller.DesiredFOV = (uint)state.OriginalFov;
                    player.Controller.DesiredFOVUpdated();
                }
            }
            
            ResetProgressBar(pawn);
            
            state.IsBerserkActive = false;
            player.SendCenter(T(player, "BerserkSkillEnded"));
        }
    }

    public void ApplyEffect(IPlayer player, float Time, int fov)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        SetProgressBar(pawn, Time);
        SetFov(player, fov);
    }

    public void SetFov(IPlayer player, int fov)
    {
        if (player == null || !player.IsValid)
            return;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid)
            return;

        if (controller.DesiredFOV != fov)
        {
            controller.DesiredFOV = (uint)fov;
            controller.DesiredFOVUpdated();
        }
    }

    public void SetProgressBar(CCSPlayerPawn pawn, float time)
    {
        if(pawn == null || !pawn.IsValid)
            return;

        ResetProgressBar(pawn);

        CSPlayerBlockingUseAction_t type = CSPlayerBlockingUseAction_t.k_CSPlayerBlockingUseAction_None;

        int timeInt = (int)Math.Ceiling(time);
        //pawn.SimulationTime = _core.Engine.GlobalVars.CurrentTime + time;
        pawn.ProgressBarDuration = timeInt;
        pawn.BlockingUseActionInProgress = type;
        pawn.ProgressBarStartTime = _core.Engine.GlobalVars.CurrentTime;
        
        //pawn.SimulationTimeUpdated();
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

    public void IdleSound(IPlayer player, string sound)
    {
        if (player == null || !player.IsValid)
            return;

        if (!_globals.PlayerSkillStates.TryGetValue(player.PlayerID, out var state))
            return;

        if (!state.IsBerserkActive)
        {
            state.IsIdleSoundRunning = false;
            return;
        }

        if (!state.IsIdleSoundRunning)
            state.IsIdleSoundRunning = true;

        EmitSoundFormPlayer(player, sound, 1.0f);

        _core.Scheduler.DelayBySeconds(2.5f, () =>
        {
            if (player == null || !player.IsValid)
                return;

            if (!_globals.PlayerSkillStates.TryGetValue(player.PlayerID, out var nextState))
                return;

            if (!nextState.IsBerserkActive)
            {
                nextState.IsIdleSoundRunning = false;
                return;
            }

            IdleSound(player, sound);
        });
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
}
