using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_TongueGrab_Service
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_TongueGrab_Helpers _helpers;
    private readonly HZP_ZombieSkill_TongueGrab_Config _config;
    private readonly HZP_ZombieSkill_TongueGrab_Globals _globals;

    public HZP_ZombieSkill_TongueGrab_Service(
        ISwiftlyCore core,
        HZP_ZombieSkill_TongueGrab_Helpers helpers,
        IOptions<HZP_ZombieSkill_TongueGrab_Config> config,
        HZP_ZombieSkill_TongueGrab_Globals globals)
    {
        _core = core;
        _helpers = helpers;
        _config = config.Value;
        _globals = globals;
    }

    public void HookEvent()
    {
        _core.Event.OnClientKeyStateChanged += OnButtonChange;
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.Event.OnMapUnload += Event_OnMapUnload;
        _core.Event.OnTick += Event_OnTick;

        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPost<EventPlayerTeam>(OnPlayerTeam);
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
    }

    public void CleanupOnUnload()
    {
        _helpers.CleanupAllState();
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var playerId = player.PlayerID;
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return HookResult.Continue;

        var resetCooldown = state.LastUsedPayload?.DeathRefresh == true;
        _helpers.ResetPlayerSkill(player, resetCooldown, cancelCooldownTimer: resetCooldown);

        if (resetCooldown)
        {
            _globals.PlayerSkillStates.Remove(playerId);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        if (@event.Disconnect || @event.OldTeam == @event.Team)
            return HookResult.Continue;

        var playerId = @event.UserId;
        _core.Scheduler.NextTick(() =>
        {
            if (_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            {
                var player = _core.PlayerManager.GetPlayer(playerId);
                if (player != null && player.IsValid && (state.CasterSessionId == 0 || player.SessionId == state.CasterSessionId))
                {
                    _helpers.ResetPlayerSkill(player);
                }
                else
                {
                    _helpers.ResetPlayerSkillState(playerId);
                }

                _globals.PlayerSkillStates.Remove(playerId);
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _helpers.CleanupAllState();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _helpers.CleanupAllState();
        return HookResult.Continue;
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _helpers.ResetPlayerSkillState(@event.PlayerId);
        _globals.PlayerSkillStates.Remove(@event.PlayerId);
    }

    private void Event_OnMapUnload(IOnMapUnloadEvent @event)
    {
        _helpers.CleanupAllState();
    }

    private void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        _helpers.AddPrecacheResources(@event, _config.Groups);
    }

    private void Event_OnTick()
    {
        _helpers.UpdateActiveTongueGrabs();
    }

    private void OnButtonChange(IOnClientKeyStateChangedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid || !player.IsAlive)
            return;

        if (!player.PressedButtons.HasFlag(GameButtonFlags.R))
            return;

        var zpApi = HZP_ZombieSkill_TongueGrab.ZpApi;
        if (zpApi == null || !zpApi.HZP_IsZombie(player.PlayerID))
            return;

        var group = GetEnabledGroup(player);
        if (group == null)
            return;

        var state = _helpers.GetOrCreatePlayerState(player.PlayerID, player.SessionId);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        if (currentTime - state.LastButtonPressTime < 0.1f)
            return;

        state.LastButtonPressTime = currentTime;

        if (state.IsActive)
        {
            player.SendCenter(_helpers.T(player, "TongueGrabAlreadyActive"));
            return;
        }

        if (currentTime < state.CooldownEndTime)
        {
            var remaining = Math.Max(0.0f, state.CooldownEndTime - currentTime);
            player.SendCenter(_helpers.T(player, "TongueGrabCooldown", remaining));
            return;
        }

        if (!_helpers.TryFindTarget(player, group, out var target) || target == null)
        {
            _helpers.ShowMissTongueBeam(player, group);
            _helpers.StartMissCooldown(player, group);
            return;
        }

        _helpers.GiveTongueGrab(player, target, group);
    }

    private TongueGrabSkillGroup? GetEnabledGroup(IPlayer player)
    {
        var zpApi = HZP_ZombieSkill_TongueGrab.ZpApi;
        if (zpApi == null)
            return null;

        var zombieClassName = zpApi.HZP_GetZombieClassname(player);
        return _config.Groups.FirstOrDefault(group =>
            group.Enable &&
            string.Equals(group.Name, zombieClassName, StringComparison.Ordinal));
    }
}
