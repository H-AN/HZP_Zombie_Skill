using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_DeathExplosion_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_DeathExplosion_Globals _globals;

    public HZP_ZombieSkill_DeathExplosion_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_DeathExplosion_Globals globals,
        IOptionsMonitor<HZP_ZombieSkill_DeathExplosion_Config> _)
    {
        _core = core;
        _globals = globals;
    }

    public DeathExplosionPlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new DeathExplosionPlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public void TriggerDeathExplosion(IPlayer attacker, Vector explosionPosition, DeathExplosionSkillGroup group)
    {
        if (attacker == null || !attacker.IsValid)
            return;

        var zpApi = HZP_ZombieSkill_DeathExplosion.ZpApi;
        if (zpApi == null)
            return;

        var attackerPawn = attacker.PlayerPawn;
        var payload = BuildPayload(group);

        PlayExplosionParticle(explosionPosition, payload);

        if (attackerPawn != null && attackerPawn.IsValid)
        {
            EmitSoundFromEntity((int)attackerPawn.Index, payload.SoundExplode, 1.0f);
        }

        foreach (var target in _core.PlayerManager.GetAlive())
        {
            if (target == null || !target.IsValid || !target.IsAlive)
                continue;

            if (target.PlayerID == attacker.PlayerID)
                continue;

            if (zpApi.HZP_IsZombie(target.PlayerID))
                continue;

            var targetPawn = target.PlayerPawn;
            var targetPosition = targetPawn?.AbsOrigin;
            if (targetPawn == null || !targetPawn.IsValid || targetPosition == null)
                continue;

            var distance = GetDistance(explosionPosition, targetPosition.Value);
            if (distance > payload.Radius)
                continue;

            var damage = payload.Damage * CalculateDamageScale(
                distance,
                payload.Radius,
                payload.UseDistanceFalloff,
                payload.MinimumDamageScale);

            if (damage <= 0.0f)
                continue;

            ApplyDamage(attacker, target, damage, DamageTypes_t.DMG_BLAST);
        }
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<DeathExplosionSkillGroup> groups)
    {
        foreach (var group in groups)
        {
            if (!group.Enable)
                continue;

            if (!string.IsNullOrWhiteSpace(group.PrecacheSound))
            {
                foreach (var resource in group.PrecacheSound.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    @event.AddItem(resource);
                }
            }

            if (string.IsNullOrWhiteSpace(group.ExplosionParticle))
                continue;

            @event.AddItem(group.ExplosionParticle);
        }
    }

    public void ResetPlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
            return;

        state.LastButtonPressTime = 0.0f;
    }

    public void CleanupAllState()
    {
        foreach (var entityId in _globals.ActiveParticleEntityIds.ToList())
        {
            KillParticle(entityId);
        }

        _globals.ActiveParticleEntityIds.Clear();
        _globals.PlayerSkillStates.Clear();
    }

    public string T(IPlayer? player, string key, params object[] args)
    {
        if (player == null || !player.IsValid)
            return string.Format(key, args);

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        return localizer[key, args];
    }

    private DeathExplosionPayload BuildPayload(DeathExplosionSkillGroup group)
    {
        return new DeathExplosionPayload
        {
            Damage = Math.Max(0.0f, group.Damage),
            Radius = MathF.Max(1.0f, group.Radius),
            UseDistanceFalloff = group.UseDistanceFalloff,
            MinimumDamageScale = Math.Clamp(group.MinimumDamageScale, 0.0f, 1.0f),
            ExplosionParticle = group.ExplosionParticle ?? string.Empty,
            ParticleLifetime = Math.Max(0.1f, group.ParticleLifetime),
            SoundExplode = group.SoundExplode ?? string.Empty
        };
    }

    private void PlayExplosionParticle(Vector position, DeathExplosionPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.ExplosionParticle))
            return;

        var particle = _core.EntitySystem.CreateEntityByDesignerName<CParticleSystem>("info_particle_system");
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return;

        particle.StartActive = true;
        particle.EffectName = payload.ExplosionParticle;
        particle.DispatchSpawn();
        particle.Teleport(position, QAngle.Zero, Vector.Zero);
        particle.AcceptInput("Start", "");

        var particleEntityId = (int)particle.Index;
        _globals.ActiveParticleEntityIds.Add(particleEntityId);

        _core.Scheduler.DelayBySeconds(payload.ParticleLifetime, () =>
        {
            KillParticle(particleEntityId);
        });
    }

    private void ApplyDamage(IPlayer attacker, IPlayer target, float damageAmount, DamageTypes_t damageType)
    {
        if (attacker == null || !attacker.IsValid)
            return;

        if (target == null || !target.IsValid)
            return;

        var attackerPawn = attacker.PlayerPawn;
        var targetPawn = target.PlayerPawn;
        if (attackerPawn == null || !attackerPawn.IsValid || targetPawn == null || !targetPawn.IsValid)
            return;

        CBaseEntity inflictorEntity = attackerPawn;
        CBaseEntity attackerEntity = attackerPawn;
        CBaseEntity abilityEntity = attackerPawn;

        var damageInfo = new CTakeDamageInfo(inflictorEntity, attackerEntity, abilityEntity, damageAmount, damageType);
        damageInfo.DamageForce = new Vector(0.0f, 0.0f, 10.0f);

        var targetPosition = targetPawn.AbsOrigin;
        if (targetPosition != null)
        {
            damageInfo.DamagePosition = targetPosition.Value;
        }

        target.TakeDamage(damageInfo);
    }

    private void EmitSoundFromEntity(int entityIndex, string soundPath, float volume)
    {
        var finalSound = RandomSelectSound(soundPath);
        if (string.IsNullOrWhiteSpace(finalSound))
            return;

        var sound = new SoundEvent(finalSound, volume, 1.0f)
        {
            SourceEntityIndex = entityIndex
        };
        sound.Recipients.AddAllPlayers();

        _core.Scheduler.NextTick(() =>
        {
            sound.Emit();
        });
    }

    private void KillParticle(int entityId)
    {
        _globals.ActiveParticleEntityIds.Remove(entityId);

        var particle = _core.EntitySystem.GetEntityByIndex<CParticleSystem>((uint)entityId);
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return;

        particle.AcceptInput("Kill", 0);
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

    private static float GetDistance(Vector left, Vector right)
    {
        var deltaX = left.X - right.X;
        var deltaY = left.Y - right.Y;
        var deltaZ = left.Z - right.Z;

        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
    }

    private static float CalculateDamageScale(
        float distance,
        float radius,
        bool useDistanceFalloff,
        float minimumDamageScale)
    {
        if (!useDistanceFalloff || radius <= 0.0f)
            return 1.0f;

        var normalized = 1.0f - Math.Clamp(distance / radius, 0.0f, 1.0f);
        return MathF.Max(minimumDamageScale, normalized);
    }
}
