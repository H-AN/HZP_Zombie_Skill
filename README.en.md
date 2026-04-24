<div align="center">
  <h1>HZP Zombie Skill Pack</h1>
  <p><strong>A CS2 zombie skill collection built for SwiftlyS2 and the HanZombiePlague API</strong></p>
  <p>Provides standalone active and passive skill modules with per-class binding, per-skill cooldowns, and separate config files.</p>
</div>

---

[![cn](https://flagcdn.com/48x36/cn.png) 中文版](./README.md)
[![en](https://flagcdn.com/48x36/gb.png) English](./README.en.md)

---

## Overview

`HZP_Zombie_Skill` is an extension repository for `HanZombiePlagueS2`.  
It contains 9 zombie skill plugins that can be enabled, built, and deployed independently.

Each skill is bound to a specific zombie class through `Groups.Name`, and each module has its own config file for cooldowns, duration, particles, sounds, damage, radius, and other behavior.

This repository is a good fit if you want to:

- add unique gameplay skills to different zombie classes
- keep skills modular instead of putting everything into one large plugin
- expand `HanZombiePlagueS2` without rewriting the core mode plugin

## Highlights

- 9 standalone skill modules that can be compiled and deployed separately
- Uses the shared `HanZombiePlague` interface to detect zombie state and class name
- Active skills use a consistent default trigger key: `R`
- Most skills support `DeathRefresh` to reset cooldown/state on death
- Every plugin ships with both `zh-CN` and `en` translation resources
- Example configs are already organized under `configs/plugins/`

## Included Skills

| Plugin | Type | Description | Config file |
| --- | --- | --- | --- |
| `HZP_ZombieSkill_Berserk` | Active | Temporarily boosts movement speed and adjusts FOV for a berserk state | `configs/plugins/HZP_ZombieSkill_Berserk/HZPZombieSkillBerserk.jsonc` |
| `HZP_ZombieSkill_Hiding` | Active | Applies an invisibility-like stealth effect by lowering player alpha | `configs/plugins/HZP_ZombieSkill_Hiding/HZPZombieSkillHiding.jsonc` |
| `HZP_ZombieSkill_HealingAura` | Active | Heals nearby zombies, including the caster, with visual feedback | `configs/plugins/HZP_ZombieSkill_HealingAura/HZPZombieSkillHealingAura.jsonc` |
| `HZP_ZombieSkill_DamageReduction` | Active | Reduces incoming human damage while the state is active | `configs/plugins/HZP_ZombieSkill_DamageReduction/HZPZombieSkillDamageReduction.jsonc` |
| `HZP_ZombieSkill_Pounce` | Active | Hunter-style forward leap skill | `configs/plugins/HZP_ZombieSkill_Pounce/HZPZombieSkillPounce.jsonc` |
| `HZP_ZombieSkill_TongueGrab` | Active | Hooks a human in front and drags them toward the zombie | `configs/plugins/HZP_ZombieSkill_TongueGrab/HZPZombieSkillTongueGrab.jsonc` |
| `HZP_ZombieSkill_DisarmGrenade` | Active | Grants a custom decoy grenade that forces humans to drop their primary weapon on hit | `configs/plugins/HZP_ZombieSkill_DisarmGrenade/HZPZombieSkillDisarmGrenade.jsonc` |
| `HZP_ZombieSkill_ShockwaveGrenade` | Active | Grants a custom grenade that shakes and knocks back nearby humans | `configs/plugins/HZP_ZombieSkill_ShockwaveGrenade/HZPZombieSkillShockwaveGrenade.jsonc` |
| `HZP_ZombieSkill_DeathExplosion` | Passive | Explodes on death and damages nearby humans | `configs/plugins/HZP_ZombieSkill_DeathExplosion/HZPZombieSkillDeathExplosion.jsonc` |

## Skill Trigger Behavior

- All active skills in this repository use `R` by default
- `HealingAura`, `Berserk`, `Hiding`, `DamageReduction`, `Pounce`, and `TongueGrab` activate immediately on `R`
- `DisarmGrenade` and `ShockwaveGrenade` grant a special throwable on `R`, then the effect happens when the projectile is used
- `DeathExplosion` is passive; pressing `R` only shows a usage hint

## Configuration Rules

Each plugin has its own root config section, for example:

- `HZPZombieSkillBerserkCFG`
- `HZPZombieSkillPounceCFG`
- `HZPZombieSkillTongueGrabCFG`

These configs generally follow the same pattern:

1. Each item inside `Groups` defines one zombie-class-specific skill setup.
2. `Name` must match the zombie class name returned by `HanZombiePlagueS2` exactly.
3. `Enable` toggles the skill for that class.
4. `DeathRefresh` controls whether the skill resets on death.
5. Each plugin also exposes its own custom fields, such as:
   - `Berserk`: `SpeedMultiplier`
   - `Hiding`: `Alpha`
   - `HealingAura`: `HealAmount`, `Radius`
   - `Pounce`: `ForwardForce`, `VerticalForce`
   - `TongueGrab`: `MaxGrabDistance`, `PullSpeed`
   - `ShockwaveGrenade`: `ExplosionRadius`, `KnockbackHorizontal`
   - `DeathExplosion`: `Damage`, `Radius`

If you want to bind a skill to a new zombie class, the most important part is making sure `Groups.Name` matches the class name exactly.

## Installation

1. Make sure `SwiftlyS2` is already installed and working.
2. Make sure `HanZombiePlagueS2` is loaded and exposes the shared interface named `HanZombiePlague`.
3. Build only the skill plugins you want to use.
4. Deploy the generated plugin files into `addons/swiftlys2/plugins/`.
5. Put the matching config files into `configs/plugins/<plugin-folder>/`.
6. Restart the server or hot reload the plugins and test the skill behavior in game.

Notes:

- This repository is a skill pack, not a mandatory all-in-one plugin
- You can install only one module or combine multiple modules as needed

## Repository Layout

```text
HZP_Zombie_Skill/
├─ src/
│  ├─ HZP_ZombieSkill_Berserk/
│  ├─ HZP_ZombieSkill_Hiding/
│  ├─ HZP_ZombieSkill_HealingAura/
│  ├─ HZP_ZombieSkill_DamageReduction/
│  ├─ HZP_ZombieSkill_Pounce/
│  ├─ HZP_ZombieSkill_TongueGrab/
│  ├─ HZP_ZombieSkill_DisarmGrenade/
│  ├─ HZP_ZombieSkill_ShockwaveGrenade/
│  ├─ HZP_ZombieSkill_DeathExplosion/
│  └─ api/HanZombiePlagueAPI.dll
└─ configs/plugins/
   └─ example configs for each skill
```

## Build Notes

All plugin projects currently target `net10.0` and depend on:

- `SwiftlyS2.CS2`
- `src/api/HanZombiePlagueAPI.dll`

Build any individual module on demand, for example:

```powershell
dotnet build src/HZP_ZombieSkill_TongueGrab/HZP_ZombieSkill_TongueGrab.csproj
```

If you only want a subset of the skills, just build and deploy those modules.

## Dependencies

- Required main plugin: `HanZombiePlagueS2`
- Shared interface name: `HanZombiePlague`
- Runtime environment: `SwiftlyS2`
- Current target framework in source: `net10.0`

If the main plugin does not expose the `HanZombiePlague` shared interface correctly, these skill plugins will fail to load.

## Good Next Extensions

- add more class-specific skills using the same `Groups.Name` binding model
- expand particles, sounds, and hit feedback for existing skills
- add admin/debug commands or permission checks
- create mode-specific parameter presets for different zombie events


