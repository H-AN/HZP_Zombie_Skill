using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_DisarmGrenade_Service
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_DisarmGrenade_Helpers _helpers;
    private readonly HZP_ZombieSkill_DisarmGrenade_Config _config;
    private readonly HZP_ZombieSkill_DisarmGrenade_Globals _globals;

    public HZP_ZombieSkill_DisarmGrenade_Service(
        ISwiftlyCore core,
        HZP_ZombieSkill_DisarmGrenade_Helpers helpers,
        IOptions<HZP_ZombieSkill_DisarmGrenade_Config> config,
        HZP_ZombieSkill_DisarmGrenade_Globals globals)
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
        _core.Event.OnEntityCreated += Event_OnEntityCreated;
        _core.Event.OnEntityTakeDamage += Event_OnEntityTakeDamage;

        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.GameEvent.HookPost<EventPlayerTeam>(OnPlayerTeam);
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _core.GameEvent.HookPre<EventDecoyFiring>(OnDecoyFiring);
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

        _helpers.CleanupPlayerProjectiles(playerId);

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
            var zpApi = HZP_ZombieSkill_DisarmGrenade.ZpApi;
            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid)
            {
                if (zpApi == null || !zpApi.HZP_IsZombie(playerId))
                {
                    _helpers.DropGrenadeSlot(player);
                }

                _helpers.CleanupPlayerProjectiles(playerId);
                _helpers.ResetPlayerSkill(player);
            }
            else
            {
                _helpers.CleanupPlayerProjectiles(playerId);
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

    private HookResult OnDecoyFiring(EventDecoyFiring @event)
    {
        var entityId = @event.EntityID;
        var entity = _core.EntitySystem.GetEntityByIndex<CDecoyProjectile>((uint)entityId);
        if (entity != null && entity.IsValid && entity.IsValidEntity)
        {
            _helpers.TryBindDisarmProjectile(entity);
        }

        _helpers.TryHandleProjectileExpiry(entityId);
        return HookResult.Continue;
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        _helpers.CleanupPlayerProjectiles(@event.PlayerId);
        _helpers.ResetPlayerSkillState(@event.PlayerId);
        _globals.PlayerSkillStates.Remove(@event.PlayerId);
    }

    private void Event_OnMapUnload(IOnMapUnloadEvent @event)
    {
        _helpers.CleanupAllState();
    }

    private void Event_OnEntityCreated(IOnEntityCreatedEvent @event)
    {
        var entity = @event.Entity;
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return;

        if (!entity.DesignerName.Equals("decoy_projectile", StringComparison.OrdinalIgnoreCase))
            return;

        _core.Scheduler.NextTick(() =>
        {
            if (entity.IsValid && entity.IsValidEntity)
            {
                _helpers.TryBindDisarmProjectile(entity);
            }
        });
    }

    private void Event_OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        if (_helpers.TryHandleProjectileDamage(@event.Entity, @event.Info.Inflictor.Value))
        {
            @event.Info.Damage = 0.0f;
        }
    }

    private void Event_OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        _helpers.AddPrecacheResources(@event, _config.Groups);
    }

    private void OnButtonChange(IOnClientKeyStateChangedEvent @event)
    {
        var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid)
            return;

        if (!player.PressedButtons.HasFlag(GameButtonFlags.R))
            return;

        var zpApi = HZP_ZombieSkill_DisarmGrenade.ZpApi;
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

        if (state.PendingGrenade != null)
        {
            player.SendCenter(_helpers.T(player, "DisarmGrenadeAlreadyOwned"));
            return;
        }

        if (currentTime < state.CooldownEndTime)
        {
            var remaining = Math.Max(0.0f, state.CooldownEndTime - currentTime);
            player.SendCenter(_helpers.T(player, "DisarmGrenadeCooldown", remaining));
            return;
        }

        _helpers.GiveDisarmGrenade(player, group);
    }

    private DisarmGrenadeSkillGroup? GetEnabledGroup(IPlayer player)
    {
        var zpApi = HZP_ZombieSkill_DisarmGrenade.ZpApi;
        if (zpApi == null)
            return null;

        var zombieClassName = zpApi.HZP_GetZombieClassname(player);
        return _config.Groups.FirstOrDefault(group =>
            group.Enable &&
            string.Equals(group.Name, zombieClassName, StringComparison.Ordinal));
    }
}
