using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_Pounce_Service
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Pounce_Helpers _helpers;
    private readonly HZP_ZombieSkill_Pounce_Config _config;
    private readonly HZP_ZombieSkill_Pounce_Globals _globals;

    public HZP_ZombieSkill_Pounce_Service(
        ISwiftlyCore core,
        HZP_ZombieSkill_Pounce_Helpers helpers,
        IOptions<HZP_ZombieSkill_Pounce_Config> config,
        HZP_ZombieSkill_Pounce_Globals globals)
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

        var resetCooldown = state.LastActivatedPayload?.DeathRefresh == true;
        _helpers.ResetPlayerSkill(player, resetCooldown);

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
            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid)
            {
                _helpers.ResetPlayerSkill(player);
            }
            else
            {
                _helpers.ResetPlayerSkillState(playerId);
            }

            _globals.PlayerSkillStates.Remove(playerId);
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

    private void OnButtonChange(IOnClientKeyStateChangedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid || !player.IsAlive)
            return;

        if (!player.PressedButtons.HasFlag(GameButtonFlags.R))
            return;

        var zpApi = HZP_ZombieSkill_Pounce.ZpApi;
        if (zpApi == null || !zpApi.HZP_IsZombie(player.PlayerID))
            return;

        var group = GetEnabledGroup(player);
        if (group == null)
            return;

        var state = _helpers.GetOrCreatePlayerState(player.PlayerID);
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        if (currentTime - state.LastButtonPressTime < 0.1f)
            return;

        state.LastButtonPressTime = currentTime;

        if (state.IsActive)
        {
            player.SendCenter(_helpers.T(player, "PounceAlreadyActive"));
            return;
        }

        if (currentTime < state.CooldownEndTime)
        {
            var remaining = Math.Max(0.0f, state.CooldownEndTime - currentTime);
            player.SendCenter(_helpers.T(player, "PounceCooldown", remaining));
            return;
        }

        if (group.RequireOnGround && !_helpers.IsPlayerOnGround(player))
        {
            player.SendCenter(_helpers.T(player, "PounceNeedGround"));
            return;
        }

        _helpers.GivePounce(player, group);
    }

    private PounceSkillGroup? GetEnabledGroup(IPlayer player)
    {
        var zpApi = HZP_ZombieSkill_Pounce.ZpApi;
        if (zpApi == null)
            return null;

        var zombieClassName = zpApi.HZP_GetZombieClassname(player);
        return _config.Groups.FirstOrDefault(group =>
            group.Enable &&
            string.Equals(group.Name, zombieClassName, StringComparison.Ordinal));
    }
}
