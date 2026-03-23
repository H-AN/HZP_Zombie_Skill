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

public class HZP_ZombieSkill_Hiding_Service
{
    private readonly ILogger<HZP_ZombieSkill_Hiding_Service> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_Hiding_Helpers _helpers;
    private readonly HZP_ZombieSkill_Hiding_Config _config;
    private readonly HZP_ZombieSkill_Hiding_Globals _globals;

    public HZP_ZombieSkill_Hiding_Service(ISwiftlyCore core, ILogger<HZP_ZombieSkill_Hiding_Service> logger,
        HZP_ZombieSkill_Hiding_Helpers helpers,IOptions<HZP_ZombieSkill_Hiding_Config> config,
        HZP_ZombieSkill_Hiding_Globals globals)
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
        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
    }

    private void Event_OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var playerId = @event.PlayerId;
        if (_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid && player.PlayerPawn != null && player.PlayerPawn.IsValid)
            {
                SetPlayerAlpha(player.PlayerPawn, 255);
            }

            state.IsHidingActive = false;
        }

        if (_globals.SkillCdTimer.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimer.Remove(playerId);
        }

        _globals.PlayerSkillStates.Remove(playerId);
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        var currentTime = _core.Engine.GlobalVars.CurrentTime;

        foreach (var kv in _globals.PlayerSkillStates)
        {
            var state = kv.Value;
            state.IsHidingActive = false;
            state.CooldownEndTime = currentTime;
            state.SkillEndTime = currentTime;

            var player = _core.PlayerManager.GetPlayer(kv.Key);
            if (player != null && player.IsValid && player.PlayerPawn != null && player.PlayerPawn.IsValid)
            {
                SetPlayerAlpha(player.PlayerPawn, 255);
            }
        }

        foreach (var kv in _globals.SkillCdTimer.ToList())
        {
            kv.Value.Cancel();
            _globals.SkillCdTimer.Remove(kv.Key);
        }

        return HookResult.Continue;
    }
    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        foreach (var kv in _globals.PlayerSkillStates)
        {
            var state = kv.Value;

            state.IsHidingActive = false;

            var player = _core.PlayerManager.GetPlayer(kv.Key);
            if (player != null && player.IsValid && player.PlayerPawn != null && player.PlayerPawn.IsValid)
            {
                SetPlayerAlpha(player.PlayerPawn, 255);
            }
        }

        foreach (var kv in _globals.SkillCdTimer.ToList())
        {
            kv.Value.Cancel();
            _globals.SkillCdTimer.Remove(kv.Key);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return HookResult.Continue;

        var _zpApi = HZP_ZombieSkill_Hiding._zpApi;
        if (_zpApi == null)
            return HookResult.Continue;

        var playerId = player.PlayerID;

        if (_globals.SkillCdTimer.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimer.Remove(playerId);
        }

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return HookResult.Continue;

        var zombieClassName = _zpApi.HZP_GetZombieClassname(player);
        var group = _config.Groups.FirstOrDefault(g => g.Enable && g.Name == zombieClassName);

        if (state.IsHidingActive)
        {
            if (group != null)
            {
                if (group.DeathRefresh)
                {
                    state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime;
                }
            }

            _helpers.ResetProgressBar(pawn);
            SetPlayerAlpha(pawn, 255);

            state.IsHidingActive = false;
        }
        else if (group != null && group.DeathRefresh)
        {
            state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime;
        }

        return HookResult.Continue;
    }

    private void SetPlayerHidingAlpha(CCSPlayerPawn pawn, int alpha)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.Render.A != (byte)alpha)
        {
            pawn.Render.A = (byte)alpha;
            pawn.RenderUpdated();
        }
    }

    private void SetPlayerAlpha(CCSPlayerPawn pawn, int alpha)
    {
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.Render.A != (byte)alpha)
        {
            pawn.Render.A = (byte)alpha;
            pawn.RenderUpdated();
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

        var _zpApi = HZP_ZombieSkill_Hiding._zpApi;
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

        if (state.IsHidingActive)
        {
            player.SendCenter(_helpers.T(player, "HidingSkillAlreadyActive"));
            return;
        }

        if (currentTime < state.CooldownEndTime)
        {
            float remaining = Math.Max(0, state.CooldownEndTime - currentTime);
            player.SendCenter(_helpers.T(player, "HidingSkillCooldown", remaining));
            return;
        }

        _helpers.GiveHidingSkill(player, group);
    }
}
