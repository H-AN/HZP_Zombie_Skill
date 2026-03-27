using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_ShockwaveGrenade_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_ShockwaveGrenade_Globals _globals;

    public HZP_ZombieSkill_ShockwaveGrenade_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_ShockwaveGrenade_Globals globals)
    {
        _core = core;
        _globals = globals;
    }

    public ShockwaveGrenadePlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new ShockwaveGrenadePlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public void GiveShockwaveGrenade(IPlayer player, ShockwaveGrenadeSkillGroup group)
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

        grenade.Entity!.Name = "ShockwaveGrenade";
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

        player.SendCenter(T(player, "ShockwaveGrenadeGranted"));
        EmitSoundFromPlayer(player, payload.SoundGive, 1.0f);

        ScheduleCooldownReadyNotice(playerId, payload.Cooldown);
    }

    public void TryBindShockwaveProjectile(CEntityInstance entity)
    {
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return;

        if (!entity.DesignerName.Equals("decoy_projectile", StringComparison.OrdinalIgnoreCase))
            return;

        var grenade = entity.As<CBaseCSGrenadeProjectile>();
        if (grenade == null || !grenade.IsValid || !grenade.IsValidEntity)
            return;

        if (!grenade.Thrower.IsValid || grenade.Thrower.Value == null || !grenade.Thrower.Value.IsValid)
            return;

        var player = _core.PlayerManager.GetPlayerFromPawn(grenade.Thrower.Value);
        if (player == null || !player.IsValid)
            return;

        if (!_globals.PlayerSkillStates.TryGetValue(player.PlayerID, out var state))
            return;

        if (state.PendingGrenade == null)
            return;

        _globals.ActiveProjectiles[(int)entity.Index] = new ActiveShockwaveProjectileState
        {
            ThrowerPlayerId = player.PlayerID,
            Payload = state.PendingGrenade
        };

        ApplyProjectileModel(grenade, state.PendingGrenade);
        AttachTrailParticle(grenade, state.PendingGrenade);

        state.PendingGrenade = null;

        if (_core.Engine.GlobalVars.CurrentTime >= state.CooldownEndTime)
        {
            CancelCooldownTimer(player.PlayerID);
            _globals.PlayerSkillStates.Remove(player.PlayerID);
        }
    }

    public bool TryHandleShockwaveDetonation(int entityId, Vector position)
    {
        if (!_globals.ActiveProjectiles.Remove(entityId, out var projectile))
            return false;

        KillProjectileTrail(entityId);

        var payload = projectile.Payload;
        var zpApi = HZP_ZombieSkill_ShockwaveGrenade.ZpApi;
        var thrower = _core.PlayerManager.GetPlayer(projectile.ThrowerPlayerId);

        // Mirror the teleport grenade's current semantics:
        // if the thrower is no longer a zombie when the decoy fires, this zombie skill grenade is discarded.
        if (thrower == null
            || !thrower.IsValid
            || zpApi == null
            || !zpApi.HZP_IsZombie(thrower.PlayerID))
        {
            return true;
        }

        DrawExpandingRing(
            position,
            payload.ExplosionRadius,
            payload.RingColorR,
            payload.RingColorG,
            payload.RingColorB,
            payload.RingColorA,
            payload.RingDuration,
            payload.RingSegments,
            payload.RingThickness);

        EmitSoundFromEntity(entityId, payload.SoundExplode, 1.0f);

        var allPlayers = _core.PlayerManager.GetAlive();
        foreach (var target in allPlayers)
        {
            if (target == null || !target.IsValid)
                continue;

            if (zpApi != null && zpApi.HZP_IsZombie(target.PlayerID))
                continue;

            var pawn = target.PlayerPawn;
            if (pawn == null || !pawn.IsValid)
                continue;

            var targetPos = pawn.AbsOrigin;
            if (targetPos == null)
                continue;

            var distance = GetDistance(position, targetPos.Value);
            if (distance > payload.ExplosionRadius)
                continue;

            var effectScale = CalculateEffectScale(
                distance,
                payload.ExplosionRadius,
                payload.UseDistanceFalloff,
                payload.MinEffectScale);
            if (effectScale <= 0.0f)
                continue;

            ApplyShake(target, payload.ShakeAmplitude, payload.ShakeFrequency, payload.ShakeDuration, effectScale);
            ApplyKnockback(pawn, position, payload.KnockbackHorizontal, payload.KnockbackVertical, effectScale);
        }

        if (payload.AllowThrowerSelfAffect)
        {
            if (thrower != null && thrower.IsValid)
            {
                var throwerPawn = thrower.PlayerPawn;
                var throwerPos = throwerPawn?.AbsOrigin;

                if (throwerPawn != null && throwerPawn.IsValid && throwerPos != null)
                {
                    var selfRadius = payload.SelfEffectRadius > 0.0f
                        ? payload.SelfEffectRadius
                        : payload.ExplosionRadius;

                    var selfDistance = GetDistance(position, throwerPos.Value);
                    if (selfDistance <= selfRadius)
                    {
                        var selfEffectScale = CalculateEffectScale(
                            selfDistance,
                            selfRadius,
                            payload.SelfUseDistanceFalloff,
                            payload.SelfMinEffectScale);

                        if (selfEffectScale > 0.0f)
                        {
                            ApplyShake(
                                thrower,
                                payload.SelfShakeAmplitude,
                                payload.SelfShakeFrequency,
                                payload.SelfShakeDuration,
                                selfEffectScale);

                            ApplyKnockback(
                                throwerPawn,
                                position,
                                payload.SelfKnockbackHorizontal,
                                payload.SelfKnockbackVertical,
                                selfEffectScale);
                        }
                    }
                }
            }
        }

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
            KillProjectile(entityId);
        }

        foreach (var playerId in _globals.SkillCdTimers.Keys.ToList())
        {
            CancelCooldownTimer(playerId);
        }

        _globals.ActiveProjectiles.Clear();
        _globals.ProjectileTrailParticles.Clear();
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
            KillProjectile(entityId);
            _globals.ActiveProjectiles.Remove(entityId);
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

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<ShockwaveGrenadeSkillGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.Enable || string.IsNullOrWhiteSpace(group.PrecacheSound))
                continue;

            foreach (var resource in group.PrecacheSound.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                @event.AddItem(resource);
            }

            if (!string.IsNullOrWhiteSpace(group.TrailParticle))
            {
                @event.AddItem(group.TrailParticle);
            }

            if (!string.IsNullOrWhiteSpace(group.ProjectileModel))
            {
                @event.AddItem(group.ProjectileModel);
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

    private ShockwaveGrenadePayload CreatePayload(ShockwaveGrenadeSkillGroup group)
    {
        return new ShockwaveGrenadePayload
        {
            DeathRefresh = group.DeathRefresh,
            ReplaceGrenadeSlot = group.ReplaceGrenadeSlot,
            AllowThrowerSelfAffect = group.AllowThrowerSelfAffect,
            Cooldown = group.Cooldown,
            ExplosionRadius = MathF.Max(1.0f, group.ExplosionRadius),
            ShakeAmplitude = MathF.Max(0.0f, group.ShakeAmplitude),
            ShakeFrequency = MathF.Max(0.0f, group.ShakeFrequency),
            ShakeDuration = MathF.Max(0.0f, group.ShakeDuration),
            KnockbackHorizontal = MathF.Max(0.0f, group.KnockbackHorizontal),
            KnockbackVertical = MathF.Max(0.0f, group.KnockbackVertical),
            UseDistanceFalloff = group.UseDistanceFalloff,
            MinEffectScale = Math.Clamp(group.MinEffectScale, 0.0f, 1.0f),
            SelfEffectRadius = group.SelfEffectRadius <= 0.0f
                ? MathF.Max(1.0f, group.ExplosionRadius)
                : group.SelfEffectRadius,
            SelfShakeAmplitude = MathF.Max(0.0f, group.SelfShakeAmplitude),
            SelfShakeFrequency = MathF.Max(0.0f, group.SelfShakeFrequency),
            SelfShakeDuration = MathF.Max(0.0f, group.SelfShakeDuration),
            SelfKnockbackHorizontal = MathF.Max(0.0f, group.SelfKnockbackHorizontal),
            SelfKnockbackVertical = MathF.Max(0.0f, group.SelfKnockbackVertical),
            SelfUseDistanceFalloff = group.SelfUseDistanceFalloff,
            SelfMinEffectScale = Math.Clamp(group.SelfMinEffectScale, 0.0f, 1.0f),
            RingColorR = Math.Clamp(group.RingColorR, 0, 255),
            RingColorG = Math.Clamp(group.RingColorG, 0, 255),
            RingColorB = Math.Clamp(group.RingColorB, 0, 255),
            RingColorA = Math.Clamp(group.RingColorA, 0, 255),
            RingDuration = MathF.Max(0.05f, group.RingDuration),
            RingSegments = Math.Max(3, group.RingSegments),
            RingThickness = MathF.Max(1.0f, group.RingThickness),
            TrailParticle = group.TrailParticle,
            ProjectileModel = group.ProjectileModel,
            WeaponCustomName = string.IsNullOrWhiteSpace(group.WeaponCustomName)
                ? "zombie_shockwave_grenade"
                : group.WeaponCustomName,
            SoundGive = group.SoundGive,
            SoundExplode = group.SoundExplode
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
                player.SendCenter(T(player, "ShockwaveGrenadeReady"));
            }

            _globals.PlayerSkillStates.Remove(playerId);
        });
    }

    private static float GetDistance(Vector left, Vector right)
    {
        var deltaX = left.X - right.X;
        var deltaY = left.Y - right.Y;
        var deltaZ = left.Z - right.Z;

        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
    }

    private static float CalculateEffectScale(
        float distance,
        float radius,
        bool useDistanceFalloff,
        float minEffectScale)
    {
        if (!useDistanceFalloff || radius <= 0.0f)
            return 1.0f;

        var normalized = 1.0f - Math.Clamp(distance / radius, 0.0f, 1.0f);
        return MathF.Max(minEffectScale, normalized);
    }

    private void ApplyShake(
        IPlayer target,
        float shakeAmplitude,
        float shakeFrequency,
        float shakeDuration,
        float scale)
    {
        if (target == null || !target.IsValid)
            return;

        var pawn = target.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var controller = target.Controller;
        if (controller == null || !controller.IsValid)
            return;

        if (controller.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        if (shakeAmplitude <= 0.0f || shakeDuration <= 0.0f)
            return;

        var shake = _core.NetMessage.Create<CUserMessageShake>();
        shake.Amplitude = shakeAmplitude * scale;
        shake.Frequency = shakeFrequency;
        shake.Duration = shakeDuration;
        shake.SendToPlayer(target.PlayerID);
    }

    private void ApplyKnockback(
        CCSPlayerPawn targetPawn,
        Vector explosionPosition,
        float knockbackHorizontal,
        float knockbackVertical,
        float scale)
    {
        if (targetPawn == null || !targetPawn.IsValid)
            return;

        var targetPos = targetPawn.AbsOrigin;
        if (targetPos == null)
            return;

        var directionX = targetPos.Value.X - explosionPosition.X;
        var directionY = targetPos.Value.Y - explosionPosition.Y;
        var horizontalLength = MathF.Sqrt(directionX * directionX + directionY * directionY);

        if (horizontalLength > 0.001f)
        {
            directionX /= horizontalLength;
            directionY /= horizontalLength;
        }
        else
        {
            directionX = 0.0f;
            directionY = 0.0f;
        }

        if (knockbackHorizontal <= 0.0f && knockbackVertical <= 0.0f)
            return;

        var pushVelocity = new Vector(
            directionX * knockbackHorizontal * scale,
            directionY * knockbackHorizontal * scale,
            knockbackVertical * scale);

        targetPawn.Teleport(null, null, targetPawn.AbsVelocity + pushVelocity);
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

    private void EmitSoundFromEntity(int entityIndex, string soundPath, float volume)
    {
        EmitSound(entityIndex, soundPath, volume);
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

    private void DrawExpandingRing(
        Vector position,
        float maxRadius,
        int red,
        int green,
        int blue,
        int alpha,
        float duration,
        int segments,
        float thickness)
    {
        var beams = new CBeam?[segments];
        var startTime = _core.Engine.GlobalVars.CurrentTime;

        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.PI * 2 * i / segments;
            var nextAngle = MathF.PI * 2 * (i + 1) / segments;

            var start = new Vector(
                position.X + MathF.Cos(angle),
                position.Y + MathF.Sin(angle),
                position.Z);

            var end = new Vector(
                position.X + MathF.Cos(nextAngle),
                position.Y + MathF.Sin(nextAngle),
                position.Z);

            beams[i] = CreateLaser(start, end, new SwiftlyS2.Shared.Natives.Color(red, green, blue, alpha), thickness);
        }

        CancellationTokenSource? timer = null;
        timer = _core.Scheduler.RepeatBySeconds(0.01f, () =>
        {
            var progress = MathF.Min((_core.Engine.GlobalVars.CurrentTime - startTime) / duration, 1.0f);
            var currentRadius = maxRadius * progress;

            for (var i = 0; i < segments; i++)
            {
                var beam = beams[i];
                if (beam is not { IsValid: true, IsValidEntity: true })
                    continue;

                var angle = MathF.PI * 2 * i / segments;
                var nextAngle = MathF.PI * 2 * (i + 1) / segments;

                var start = new Vector(
                    position.X + currentRadius * MathF.Cos(angle),
                    position.Y + currentRadius * MathF.Sin(angle),
                    position.Z);

                var end = new Vector(
                    position.X + currentRadius * MathF.Cos(nextAngle),
                    position.Y + currentRadius * MathF.Sin(nextAngle),
                    position.Z);

                TeleportLaser(beam, start, end);
            }

            if (progress < 1.0f)
                return;

            for (var i = 0; i < segments; i++)
            {
                var beam = beams[i];
                if (beam is not { IsValid: true, IsValidEntity: true })
                    continue;

                beam.AcceptInput("Kill", 0);
                beams[i] = null;
            }

            timer?.Cancel();
        });
    }

    private CBeam? CreateLaser(Vector start, Vector end, SwiftlyS2.Shared.Natives.Color color, float width)
    {
        var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return null;

        beam.Render = color;
        beam.Width = width;
        beam.HaloScale = 3.0f;
        beam.Teleport(start, new QAngle(), Vector.Zero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.DispatchSpawn();

        return beam;
    }

    private static void TeleportLaser(CBeam beam, Vector start, Vector end)
    {
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return;

        beam.Teleport(start, new QAngle(), Vector.Zero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.EndPosUpdated();
    }

    private void AttachTrailParticle(CBaseCSGrenadeProjectile grenade, ShockwaveGrenadePayload payload)
    {
        if (grenade == null || !grenade.IsValid || !grenade.IsValidEntity)
            return;

        if (payload == null || string.IsNullOrWhiteSpace(payload.TrailParticle))
            return;

        var particle = CreateParticleGlow(payload.TrailParticle);
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return;

        particle.AcceptInput("FollowEntity", "!activator", grenade, particle);
        _globals.ProjectileTrailParticles[(int)grenade.Index] = (int)particle.Index;
    }

    private static void ApplyProjectileModel(CBaseCSGrenadeProjectile grenade, ShockwaveGrenadePayload payload)
    {
        if (grenade == null || !grenade.IsValid || !grenade.IsValidEntity)
            return;

        if (payload == null || string.IsNullOrWhiteSpace(payload.ProjectileModel))
            return;

        grenade.SetModel(payload.ProjectileModel);
    }

    private CEnvParticleGlow? CreateParticleGlow(string particlePath)
    {
        if (string.IsNullOrWhiteSpace(particlePath))
            return null;

        var entity = _core.EntitySystem.CreateEntity<CEnvParticleGlow>();
        if (entity == null || !entity.IsValid || !entity.IsValidEntity)
            return null;

        entity.StartActive = true;
        entity.EffectName = particlePath;
        entity.RenderMode = RenderMode_t.kRenderNormal;
        entity.DispatchSpawn();
        entity.AcceptInput("Start", 0);

        return entity;
    }

    private void KillProjectile(int entityId)
    {
        KillProjectileTrail(entityId);

        var projectile = _core.EntitySystem.GetEntityByIndex<CDecoyProjectile>((uint)entityId);
        if (projectile == null || !projectile.IsValid || !projectile.IsValidEntity)
            return;

        projectile.AcceptInput("Kill", 0);
    }

    private void KillProjectileTrail(int entityId)
    {
        if (!_globals.ProjectileTrailParticles.Remove(entityId, out var trailEntityId))
            return;

        var trail = _core.EntitySystem.GetEntityByIndex<CEnvParticleGlow>((uint)trailEntityId);
        if (trail == null || !trail.IsValid || !trail.IsValidEntity)
            return;

        trail.AcceptInput("Kill", 0);
    }
}
