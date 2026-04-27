using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.AccessControl;
using System.Security.Cryptography;
using Mono.Cecil.Cil;
using Spectre.Console;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;


namespace HZP_ZombieSkill;

public class HZP_ZombieSkill_Berserk_Service
{
    private readonly ILogger<HZP_ZombieSkill_Berserk_Service> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Berserk_Helpers _helpers;
    private readonly HZP_ZombieSkill_Berserk_Config _config;
    private readonly HZP_ZombieSkill_Berserk_Globals _globals;

    public HZP_ZombieSkill_Berserk_Service(ISwiftlyCore core, ILogger<HZP_ZombieSkill_Berserk_Service> logger,
        HZP_ZombieSkill_Berserk_Helpers helpers,IOptions<HZP_ZombieSkill_Berserk_Config> config,
        HZP_ZombieSkill_Berserk_Globals globals)
    {
        _core = core;
        _logger = logger;
        _helpers = helpers;
        _config = config.Value;
        _globals = globals;
    }

    public void HookEvent()
    {
        _core.Event.OnClientKeyStateChanged += OnButtonChange;
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnTick += Event_OnTick;
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPost<EventPlayerTeam>(OnPlayerTeam);
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
    }

    private void ResetPlayerSkillForPlayerId(int playerId, bool resetCooldown = true, bool removeState = false)
    {
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player != null && player.IsValid)
        {
            _helpers.ResetPlayerSkill(player, resetCooldown);
        }
        else
        {
            _helpers.ResetPlayerSkillState(playerId, resetCooldown);
        }

        if (removeState)
        {
            _globals.PlayerSkillStates.Remove(playerId);
        }
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        if (@event.Disconnect || @event.OldTeam == @event.Team)
            return HookResult.Continue;

        var playerId = @event.UserId;
        _core.Scheduler.NextTick(() =>
        {
            ResetPlayerSkillForPlayerId(playerId, removeState: true);
        });

        return HookResult.Continue;
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var playerId = @event.PlayerId;
        _helpers.ResetPlayerSkillState(playerId);
        _globals.PlayerSkillStates.Remove(playerId);
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        foreach (var playerId in _globals.PlayerSkillStates.Keys.ToList())
        {
            ResetPlayerSkillForPlayerId(playerId, removeState: true);
        }

        return HookResult.Continue;
    }
    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        foreach (var playerId in _globals.PlayerSkillStates.Keys.ToList())
        {
            ResetPlayerSkillForPlayerId(playerId, removeState: true);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var _zpApi = HZP_ZombieSkill_Berserk._zpApi;
        if (_zpApi == null)
            return HookResult.Continue;

        var playerId = player.PlayerID;

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            _helpers.CancelCooldownTimer(playerId);
            return HookResult.Continue;
        }

        var zombieClassName = _zpApi.HZP_GetZombieClassname(player);
        var group = _config.Groups.FirstOrDefault(g => g.Enable && g.Name == zombieClassName);

        if (state.IsBerserkActive)
        {
            var resetCooldown = group?.DeathRefresh == true;
            _helpers.ResetPlayerSkill(player, resetCooldown: resetCooldown);
            if (resetCooldown)
            {
                _globals.PlayerSkillStates.Remove(playerId);
            }
        }
        else if (group != null && group.DeathRefresh)
        {
            _helpers.ResetPlayerSkillState(playerId, resetCooldown: true);
            _globals.PlayerSkillStates.Remove(playerId);
        }
        else
        {
            _helpers.CancelCooldownTimer(playerId);
        }

        return HookResult.Continue;
    }

    private void Event_OnTick()
    {
        var _zpApi = HZP_ZombieSkill_Berserk._zpApi;
        if (_zpApi == null)
            return;

        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        var allplayer = _core.PlayerManager.GetAlive();

        foreach (var player in allplayer)
        {
            if (player == null || !player.IsValid)
                continue;

            var pawn = player.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var playerId = player.PlayerID;

            if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
                continue;

            if (!_zpApi.HZP_IsZombie(playerId))
            {
                _helpers.ResetPlayerSkill(player);
                _globals.PlayerSkillStates.Remove(playerId);
                continue;
            }

            if (state.IsBerserkActive && currentTime >= state.SkillEndTime)
            {
                _helpers.ResetPlayerSkill(player, resetCooldown: false, showEndMessage: true, cancelCooldownTimer: false);
                continue;
            }

            var zombieClassName = _zpApi.HZP_GetZombieClassname(player);

            var group = _config.Groups.FirstOrDefault(g => g.Enable && g.Name == zombieClassName);
            if (group == null)
                continue;

            var zombieProperties = _zpApi.HZP_GetZombieProperties(group.Name);
            if (zombieProperties == null)
                continue;

            float targetSpeed = state.IsBerserkActive
                ? zombieProperties.Speed + group.SpeedMultiplier
                : zombieProperties.Speed;

            if (pawn.VelocityModifier != targetSpeed)
            {
                pawn.VelocityModifier = targetSpeed;
                pawn.VelocityModifierUpdated();
            }
        }
    }

    private void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        foreach (var group in _config.Groups)
        {
            if (!group.Enable)
                continue;
            
            if (string.IsNullOrEmpty(group.PrecacheSound))
                continue;

            @event.AddItem(group.PrecacheSound);
        }

    }

    public void OnButtonChange(IOnClientKeyStateChangedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);

        if (player == null || !player.IsValid)
            return;

        var _zpApi = HZP_ZombieSkill_Berserk._zpApi;
        if (_zpApi == null)
            return;

        if (!player.PressedButtons.HasFlag(GameButtonFlags.R))
            return;

        var playerId = player.PlayerID;

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new PlayerSkillState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        if (currentTime - state.LastButtonPressTime < 0.1f)
            return;

        state.LastButtonPressTime = currentTime;

        if (!_zpApi.HZP_IsZombie(playerId))
            return;

        var zombieClassName = _zpApi.HZP_GetZombieClassname(player);

        var group = _config.Groups.FirstOrDefault(g => g.Enable && g.Name == zombieClassName);
        if (group == null)
            return;

        if (state.IsBerserkActive)
        {
            player.SendCenter(_helpers.T(player, "BerserkSkillAlreadyActive"));
            return;
        }

        if (currentTime < state.CooldownEndTime)
        {
            float remaining = Math.Max(0, state.CooldownEndTime - currentTime);
            player.SendCenter(_helpers.T(player, "BerserkSkillCooldown", remaining));
            return;
        }

        _helpers.GiveBerserkSkill(player, group);
    }
}
