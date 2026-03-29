using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;

namespace HZP_ZombieSkill;

public sealed class HZP_ZombieSkill_HealingAura_Helpers
{
    private readonly ISwiftlyCore _core;
    private readonly HZP_ZombieSkill_HealingAura_Globals _globals;

    public HZP_ZombieSkill_HealingAura_Helpers(
        ISwiftlyCore core,
        HZP_ZombieSkill_HealingAura_Globals globals,
        IOptionsMonitor<HZP_ZombieSkill_HealingAura_Config> _)
    {
        _core = core;
        _globals = globals;
    }

    public HealingAuraPlayerState GetOrCreatePlayerState(int playerId)
    {
        if (!_globals.PlayerSkillStates.TryGetValue(playerId, out var state))
        {
            state = new HealingAuraPlayerState();
            _globals.PlayerSkillStates[playerId] = state;
        }

        return state;
    }

    public void TriggerHealingAura(IPlayer healer, HealingAuraSkillGroup group)
    {
        if (healer == null || !healer.IsValid)
            return;

        var zpApi = HZP_ZombieSkill_HealingAura.ZpApi;
        if (zpApi == null)
            return;

        var healerPawn = healer.PlayerPawn;
        if (healerPawn == null || !healerPawn.IsValid)
            return;

        var healPosition = healerPawn.AbsOrigin;
        if (healPosition == null)
            return;

        var payload = BuildPayload(group);

        // 绘制Beam光圈
        DrawExpandingRing(healPosition.Value, payload);

        // 播放音效
        EmitSoundFromPlayer(healer, payload.SoundStart, 1.0f);

        int healedCount = 0;
        float totalHealed = 0;

        foreach (var target in _core.PlayerManager.GetAlive())
        {
            if (target == null || !target.IsValid || !target.IsAlive)
                continue;

            if (!zpApi.HZP_IsZombie(target.PlayerID))
                continue;

            var targetPawn = target.PlayerPawn;
            var targetPosition = targetPawn?.AbsOrigin;
            if (targetPawn == null || !targetPawn.IsValid || targetPosition == null)
                continue;

            var distance = GetDistance(healPosition.Value, targetPosition.Value);
            if (distance > payload.Radius)
                continue;

            var healedAmount = ApplyHeal(target, payload.HealAmount);
            if (healedAmount > 0)
            {
                healedCount++;
                totalHealed += healedAmount;

                // 在被治疗者身上播放粒子效果（如果配置了）
                PlayTargetParticle(target, payload);
            }
        }

        if (healedCount > 0)
        {
            healer.SendCenter(T(healer, "HealingAuraSuccess", healedCount, totalHealed));
        }
        else
        {
            healer.SendCenter(T(healer, "HealingAuraNoTarget"));
        }
    }

    private float ApplyHeal(IPlayer target, float healAmount)
    {
        if (target == null || !target.IsValid)
            return 0;

        var pawn = target.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return 0;

        var currentHealth = pawn.Health;
        var maxHealth = GetMaxHealth(pawn);

        if (currentHealth >= maxHealth)
            return 0;

        var newHealth = Math.Min(currentHealth + healAmount, maxHealth);
        var actualHeal = newHealth - currentHealth;

        pawn.Health = (int)newHealth;
        pawn.HealthUpdated();

        return actualHeal;
    }

    private int GetMaxHealth(CCSPlayerPawn pawn)
    {
        if (pawn == null || !pawn.IsValid)
            return 100;

        try
        {
            var maxHealth = pawn.MaxHealth;
            return maxHealth > 0 ? maxHealth : 100;
        }
        catch
        {
            return 100;
        }
    }

    public void ScheduleCooldownReadyNotice(int playerId, float cooldownSeconds)
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

            if (_core.Engine.GlobalVars.CurrentTime < state.CooldownEndTime)
                return;

            var player = _core.PlayerManager.GetPlayer(playerId);
            if (player != null && player.IsValid)
            {
                player.SendCenter(T(player, "HealingAuraReady"));
            }
        });
    }

    public void CancelCooldownTimer(int playerId)
    {
        if (_globals.SkillCdTimers.TryGetValue(playerId, out var token))
        {
            token.Cancel();
            _globals.SkillCdTimers.Remove(playerId);
        }
    }

    public void AddPrecacheResources(IOnPrecacheResourceEvent @event, IEnumerable<HealingAuraSkillGroup> groups)
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

            // 预缓存被治疗者粒子（如果配置了）
            if (!string.IsNullOrWhiteSpace(group.TargetParticleEffect))
            {
                @event.AddItem(group.TargetParticleEffect);
            }
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
        // 清理Beam实体
        foreach (var entityId in _globals.ActiveBeamEntityIds.ToList())
        {
            KillBeam(entityId);
        }
        _globals.ActiveBeamEntityIds.Clear();

        // 清理粒子实体
        foreach (var entityId in _globals.ActiveParticleEntityIds.ToList())
        {
            KillParticle(entityId);
        }
        _globals.ActiveParticleEntityIds.Clear();

        // 清理所有定时器
        foreach (var playerId in _globals.SkillCdTimers.Keys.ToList())
        {
            CancelCooldownTimer(playerId);
        }

        _globals.PlayerSkillStates.Clear();
    }

    public string T(IPlayer? player, string key, params object[] args)
    {
        if (player == null || !player.IsValid)
            return string.Format(key, args);

        var localizer = _core.Translation.GetPlayerLocalizer(player);
        return localizer[key, args];
    }

    private HealingAuraPayload BuildPayload(HealingAuraSkillGroup group)
    {
        return new HealingAuraPayload
        {
            HealAmount = Math.Max(0.0f, group.HealAmount),
            Radius = MathF.Max(1.0f, group.Radius),
            SoundStart = group.SoundStart ?? string.Empty,
            RingColorR = Math.Clamp(group.RingColorR, 0, 255),
            RingColorG = Math.Clamp(group.RingColorG, 0, 255),
            RingColorB = Math.Clamp(group.RingColorB, 0, 255),
            RingColorA = Math.Clamp(group.RingColorA, 0, 255),
            RingDuration = Math.Max(0.05f, group.RingDuration),
            RingSegments = Math.Max(3, group.RingSegments),
            RingThickness = MathF.Max(1.0f, group.RingThickness),
            TargetParticleEffect = group.TargetParticleEffect ?? string.Empty,
            TargetParticleLifetime = Math.Max(0.1f, group.TargetParticleLifetime)
        };
    }

    // 在被治疗者身上播放粒子效果
    private void PlayTargetParticle(IPlayer target, HealingAuraPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.TargetParticleEffect))
            return;

        var pawn = target.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        // 获取玩家眼睛位置
        var eyePosition = pawn.AbsOrigin;
        if (eyePosition == null)
            return;

        // 在玩家位置上方一点播放粒子
        var particlePos = new Vector(eyePosition.Value.X, eyePosition.Value.Y, eyePosition.Value.Z + 60.0f);

        var particle = _core.EntitySystem.CreateEntityByDesignerName<CParticleSystem>("info_particle_system");
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return;

        particle.StartActive = true;
        particle.EffectName = payload.TargetParticleEffect;
        particle.DispatchSpawn();
        particle.Teleport(particlePos, QAngle.Zero, Vector.Zero);
        particle.AcceptInput("Start", "");

        var particleEntityId = (int)particle.Index;
        _globals.ActiveParticleEntityIds.Add(particleEntityId);

        _core.Scheduler.DelayBySeconds(payload.TargetParticleLifetime, () =>
        {
            KillParticle(particleEntityId);
        });
    }

    // 绘制扩散的Beam光圈
    private void DrawExpandingRing(Vector position, HealingAuraPayload payload)
    {
        var segments = payload.RingSegments;
        var beams = new CBeam?[segments];
        var startTime = _core.Engine.GlobalVars.CurrentTime;
        var color = new SwiftlyS2.Shared.Natives.Color(payload.RingColorR, payload.RingColorG, payload.RingColorB, payload.RingColorA);

        // 创建初始beam
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.PI * 2 * i / segments;
            var nextAngle = MathF.PI * 2 * (i + 1) / segments;

            var start = new Vector(
                position.X + MathF.Cos(angle),
                position.Y + MathF.Sin(angle),
                position.Z + 10.0f); // 稍微抬高一点

            var end = new Vector(
                position.X + MathF.Cos(nextAngle),
                position.Y + MathF.Sin(nextAngle),
                position.Z + 10.0f);

            beams[i] = CreateBeam(start, end, color, payload.RingThickness);
            if (beams[i] != null)
            {
                _globals.ActiveBeamEntityIds.Add((int)beams[i]!.Index);
            }
        }

        CancellationTokenSource? timer = null;
        timer = _core.Scheduler.RepeatBySeconds(0.01f, () =>
        {
            var progress = MathF.Min((_core.Engine.GlobalVars.CurrentTime - startTime) / payload.RingDuration, 1.0f);
            var currentRadius = payload.Radius * progress;

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
                    position.Z + 10.0f);

                var end = new Vector(
                    position.X + currentRadius * MathF.Cos(nextAngle),
                    position.Y + currentRadius * MathF.Sin(nextAngle),
                    position.Z + 10.0f);

                TeleportBeam(beam, start, end);
            }

            if (progress < 1.0f)
                return;

            // 结束，清理beam
            for (var i = 0; i < segments; i++)
            {
                var beam = beams[i];
                if (beam is not { IsValid: true, IsValidEntity: true })
                    continue;

                _globals.ActiveBeamEntityIds.Remove((int)beam.Index);
                beam.AcceptInput("Kill", 0);
                beams[i] = null;
            }

            timer?.Cancel();
        });
    }

    private CBeam? CreateBeam(Vector start, Vector end, SwiftlyS2.Shared.Natives.Color color, float width)
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

    private static void TeleportBeam(CBeam beam, Vector start, Vector end)
    {
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return;

        beam.Teleport(start, new QAngle(), Vector.Zero);
        beam.EndPos.X = end.X;
        beam.EndPos.Y = end.Y;
        beam.EndPos.Z = end.Z;
        beam.EndPosUpdated();
    }

    private void KillBeam(int entityId)
    {
        _globals.ActiveBeamEntityIds.Remove(entityId);

        var beam = _core.EntitySystem.GetEntityByIndex<CBeam>((uint)entityId);
        if (beam == null || !beam.IsValid || !beam.IsValidEntity)
            return;

        beam.AcceptInput("Kill", 0);
    }

    private void KillParticle(int entityId)
    {
        _globals.ActiveParticleEntityIds.Remove(entityId);

        var particle = _core.EntitySystem.GetEntityByIndex<CParticleSystem>((uint)entityId);
        if (particle == null || !particle.IsValid || !particle.IsValidEntity)
            return;

        particle.AcceptInput("Kill", 0);
    }

    private void EmitSoundFromPlayer(IPlayer player, string soundPath, float volume)
    {
        var finalSound = RandomSelectSound(soundPath);
        if (string.IsNullOrWhiteSpace(finalSound))
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var sound = new SoundEvent(finalSound, volume, 1.0f)
        {
            SourceEntityIndex = (int)pawn.Index
        };
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

    private static float GetDistance(Vector left, Vector right)
    {
        var deltaX = left.X - right.X;
        var deltaY = left.Y - right.Y;
        var deltaZ = left.Z - right.Z;

        return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
    }
}
