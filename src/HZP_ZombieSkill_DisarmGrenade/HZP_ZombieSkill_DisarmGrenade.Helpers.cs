using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_DisarmGrenade_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_DisarmGrenade_Globals _globals;

    public HZP_ZombieSkill_DisarmGrenade_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_DisarmGrenade_Globals globals)
    {
        _core = core;
        _globals = globals;
    }

    public DisarmGrenadePlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new DisarmGrenadePlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public void GiveDisarmGrenade(IPlayer player, DisarmGrenadeSkillGroup group)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var itemServices = pawn.ItemServices;
        if (itemServices == null || !itemServices.IsValid)
            return;

        var weaponServices = pawn.WeaponServices;
        if (group.ReplaceGrenadeSlot && weaponServices != null && weaponServices.IsValid)
        {
            weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_GRENADES);
        }

        var grenade = itemServices.GiveItem<CCSWeaponBase>("weapon_decoy");
        if (grenade == null || !grenade.IsValid)
            return;

        var payload = CreatePayload(group);

        grenade.Entity!.Name = "DisarmGrenade";
        grenade.AcceptInput("ChangeSubclass", "47");
        grenade.AttributeManager.Item.Initialized = true;
        grenade.AttributeManager.Item.ItemDefinitionIndex = 47;
        grenade.AttributeManager.Item.CustomName = payload.WeaponCustomName;
        grenade.AttributeManager.Item.CustomNameOverride = payload.WeaponCustomName;
        grenade.AttributeManager.Item.CustomNameUpdated();

        var playerId = player.PlayerID;
        var state = GetOrCreatePlayerState(playerId);
        state.PendingGrenade = payload;
        state.LastUsedPayload = payload;
        state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime + payload.Cooldown;

        player.SendCenter(T(player, "DisarmGrenadeGranted"));
        EmitSoundFromPlayer(player, payload.SoundGive, 1.0f);

        ScheduleCooldownReadyNotice(playerId, payload.Cooldown);
    }

    public bool TryBindDisarmProjectile(CEntityInstance entity)
    {
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return false;

        if (!entity.DesignerName.Equals("decoy_projectile", StringComparison.OrdinalIgnoreCase))
            return false;

        var entityId = (int)entity.Index;
        if (_globals.ActiveProjectiles.ContainsKey(entityId))
            return true;

        var grenade = entity.As<CBaseCSGrenadeProjectile>();
        if (grenade == null || !grenade.IsValid || !grenade.IsValidEntity)
            return false;

        if (!grenade.Thrower.IsValid || grenade.Thrower.Value == null || !grenade.Thrower.Value.IsValid)
            return false;

        var player = _core.PlayerManager.GetPlayerFromPawn(grenade.Thrower.Value);
        if (player == null || !player.IsValid)
            return false;

        if (!_globals.PlayerSkillStates.TryGetValue(player.PlayerID, out var state))
            return false;

        if (state.PendingGrenade == null)
            return false;

        _globals.ActiveProjectiles[entityId] = new ActiveDisarmProjectileState
        {
            ThrowerPlayerId = player.PlayerID,
            Payload = state.PendingGrenade
        };

        state.PendingGrenade = null;

        if (_core.Engine.GlobalVars.CurrentTime >= state.CooldownEndTime)
        {
            CancelCooldownTimer(player.PlayerID);
            _globals.PlayerSkillStates.Remove(player.PlayerID);
        }

        return true;
    }

    public bool TryHandleProjectileDamage(CEntityInstance victimEntity, CEntityInstance? inflictorEntity)
    {
        var targetPlayer = TryGetPlayerFromEntity(victimEntity);
        if (targetPlayer == null)
            return false;

        var projectileState = TryGetTrackedProjectileState(inflictorEntity, out var entityId);
        if (projectileState == null)
            return false;

        var zpApi = HZP_ZombieSkill_DisarmGrenade.ZpApi;
        var thrower = _core.PlayerManager.GetPlayer(projectileState.ThrowerPlayerId);

        if (thrower == null
            || !thrower.IsValid
            || zpApi == null
            || !zpApi.HZP_IsZombie(thrower.PlayerID))
        {
            CleanupProjectile(entityId, killProjectile: true);
            return true;
        }

        if (targetPlayer.PlayerID == thrower.PlayerID)
            return false;

        if (zpApi.HZP_IsZombie(targetPlayer.PlayerID))
            return false;

        var droppedPrimary = TryForceDropPrimaryWeapon(targetPlayer);
        if (droppedPrimary)
        {
            thrower.SendCenter(T(thrower, "DisarmGrenadeHitThrower"));
            targetPlayer.SendCenter(T(targetPlayer, "DisarmGrenadeHitVictim"));
            EmitSoundFromPlayer(targetPlayer, projectileState.Payload.SoundHit, 1.0f);
        }
        else
        {
            thrower.SendCenter(T(thrower, "DisarmGrenadeNoPrimaryThrower"));
        }

        CleanupProjectile(entityId, killProjectile: true);
        return true;
    }

    public bool TryHandleProjectileExpiry(int entityId)
    {
        if (!_globals.ActiveProjectiles.ContainsKey(entityId))
            return false;

        CleanupProjectile(entityId, killProjectile: true);
        return true;
    }

    public void ResetPlayerSkillState(int playerId, bool resetCooldown = true, bool cancelCooldownTimer = true)
    {
        if (cancelCooldownTimer)
        {
            CancelCooldownTimer(playerId);
        }

        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        state.PendingGrenade = null;
        state.LastButtonPressTime = 0.0f;

        if (resetCooldown)
        {
            state.CooldownEndTime = _core.Engine.GlobalVars.CurrentTime;
            state.LastUsedPayload = null;
        }
    }

    public void ResetPlayerSkill(IPlayer player, bool resetCooldown = true, bool cancelCooldownTimer = true)
    {
        if (player == null || !player.IsValid)
            return;

        ResetPlayerSkillState(player.PlayerID, resetCooldown, cancelCooldownTimer);
    }

    public void CancelCooldownTimer(int playerId)
    {
        if (_globals.SkillCdTimers.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimers.Remove(playerId);
        }
    }

    public void CleanupAllState()
    {
        foreach (var entityId in _globals.ActiveProjectiles.Keys.ToList())
        {
            CleanupProjectile(entityId, killProjectile: true);
        }

        foreach (var playerId in _globals.SkillCdTimers.Keys.ToList())
        {
            CancelCooldownTimer(playerId);
        }

        _globals.ActiveProjectiles.Clear();
        _globals.PlayerSkillStates.Clear();
    }

    public void CleanupPlayerProjectiles(int playerId)
    {
        var entityIds = _globals.ActiveProjectiles
            .Where(pair => pair.Value.ThrowerPlayerId == playerId)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var entityId in entityIds)
        {
            CleanupProjectile(entityId, killProjectile: true);
        }
    }

    public void DropGrenadeSlot(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var weaponServices = pawn.WeaponServices;
        if (weaponServices == null || !weaponServices.IsValid)
            return;

        weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_GRENADES);
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<DisarmGrenadeSkillGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.Enable || string.IsNullOrWhiteSpace(group.PrecacheSound))
                continue;

            foreach (var resource in group.PrecacheSound.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                @event.AddItem(resource);
            }
        }
    }

    public string T(IPlayer? player, string key, params object[] args)
    {
        if (player == null || !player.IsValid)
            return string.Format(key, args);

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        return localizer[key, args];
    }

    private DisarmGrenadePayload CreatePayload(DisarmGrenadeSkillGroup group)
    {
        return new DisarmGrenadePayload
        {
            DeathRefresh = group.DeathRefresh,
            ReplaceGrenadeSlot = group.ReplaceGrenadeSlot,
            Cooldown = MathF.Max(0.0f, group.Cooldown),
            WeaponCustomName = string.IsNullOrWhiteSpace(group.WeaponCustomName)
                ? "zombie_disarm_grenade"
                : group.WeaponCustomName,
            SoundGive = group.SoundGive,
            SoundHit = group.SoundHit
        };
    }

    private void ScheduleCooldownReadyNotice(int playerId, float cooldownSeconds)
    {
        CancelCooldownTimer(playerId);

        var token = new CancellationTokenSource();
        _globals.SkillCdTimers[playerId] = token;

        _core.Scheduler.DelayBySeconds(cooldownSeconds, () =>
        {
            if (token.IsCancellationRequested)
                return;

            _globals.SkillCdTimers.Remove(playerId);

            if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
                return;

            if (state.PendingGrenade != null)
                return;

            if (_core.Engine.GlobalVars.CurrentTime < state.CooldownEndTime)
                return;

            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid)
            {
                player.SendCenter(T(player, "DisarmGrenadeReady"));
            }

            _globals.PlayerSkillStates.Remove(playerId);
        });
    }

    private ActiveDisarmProjectileState? TryGetTrackedProjectileState(CEntityInstance? inflictorEntity, out int entityId)
    {
        entityId = 0;

        if (inflictorEntity == null || !inflictorEntity.IsValid || !inflictorEntity.IsValidEntity)
            return null;

        if (!inflictorEntity.DesignerName.Equals("decoy_projectile", StringComparison.OrdinalIgnoreCase))
            return null;

        entityId = (int)inflictorEntity.Index;
        if (_globals.ActiveProjectiles.TryGetValue(entityId, out var state))
            return state;

        TryBindDisarmProjectile(inflictorEntity);
        return _globals.ActiveProjectiles.TryGetValue(entityId, out state)
            ? state
            : null;
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

    private bool TryForceDropPrimaryWeapon(IPlayer player)
    {
        if (player == null || !player.IsValid)
            return false;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return false;

        var weaponServices = pawn.WeaponServices;
        if (weaponServices == null || !weaponServices.IsValid)
            return false;

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
                continue;

            var weapon = weaponHandle.Value.As<CCSWeaponBase>();
            if (weapon == null || !weapon.IsValid)
                continue;

            var weaponData = weapon.WeaponBaseVData;
            if (weaponData == null || weaponData.GearSlot != gear_slot_t.GEAR_SLOT_RIFLE)
                continue;

            weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_RIFLE);
            return true;
        }

        return false;
    }
    private void CleanupProjectile(int entityId, bool killProjectile)
    {
        _globals.ActiveProjectiles.Remove(entityId);

        if (killProjectile)
        {
            KillProjectile(entityId);
        }
    }

    private void KillProjectile(int entityId)
    {
        var projectile = _core.EntitySystem.GetEntityByIndex<CDecoyProjectile>((uint)entityId);
        if (projectile == null || !projectile.IsValid || !projectile.IsValidEntity)
            return;

        projectile.AcceptInput("Kill", 0);
    }

    private void EmitSoundFromPlayer(IPlayer player, string soundPath, float volume)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        EmitSound((int)pawn.Index, soundPath, volume);
    }

    private void EmitSound(int entityIndex, string soundPath, float volume)
    {
        var selectedSound = RandomSelectSound(soundPath);
        if (string.IsNullOrWhiteSpace(selectedSound))
            return;

        var sound = new SoundEvent(selectedSound, volume, 1.0f);
        sound.SourceEntityIndex = entityIndex;
        sound.Recipients.AddAllPlayers();

        _core.Scheduler.NextTick(() =>
        {
            sound.Emit();
        });
    }

    private static string RandomSelectSound(string soundPath)
    {
        if (string.IsNullOrWhiteSpace(soundPath))
            return string.Empty;

        var sounds = soundPath.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sounds.Length == 0)
            return string.Empty;

        return sounds.Length == 1
            ? sounds[0]
            : sounds[Random.Shared.Next(sounds.Length)];
    }
}
