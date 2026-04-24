<div align="center">
  <h1>HZP Zombie Skill Pack</h1>
  <p><strong>基于 SwiftlyS2 与 HanZombiePlague API 的 CS2 僵尸技能插件合集</strong></p>
  <p>为不同僵尸职业提供可独立安装、独立配置、独立冷却的主动与被动技能模块。</p>
</div>

---

[![cn](https://flagcdn.com/48x36/cn.png) 中文版](./README.md)
[![en](https://flagcdn.com/48x36/gb.png) English](./README.en.md)

---

## 项目简介

`HZP_Zombie_Skill` 是一个面向 `HanZombiePlagueS2` 的扩展技能仓库，仓库内包含 9 个可单独启用的僵尸技能插件。  
每个技能都通过 `Groups.Name` 精确绑定到指定丧尸职业，并通过独立配置文件控制持续时间、冷却、音效、粒子、伤害、范围和特殊行为。

这套仓库适合以下场景：

- 想为不同丧尸职业添加差异化技能，而不是只改血量和速度
- 想把技能拆成多个独立插件，按需启用、按需维护
- 想继续在现有 `HanZombiePlagueS2` 上扩展更多职业玩法

## 核心特点

- 9 个技能模块互相独立，可单独编译、单独部署、单独关闭
- 技能通过 `HanZombiePlague` 共享接口读取玩家是否为丧尸以及当前职业名
- 主动技能默认统一使用 `R` 键触发，玩家体验一致
- 大部分技能支持 `DeathRefresh`，可以在死亡后刷新或清理技能状态
- 每个插件自带 `zh-CN` / `en` 翻译资源，方便中英文服务器使用
- 配置样例已经按插件目录分开放在 `configs/plugins/`

## 技能列表

| 插件 | 类型 | 说明 | 配置文件 |
| --- | --- | --- | --- |
| `HZP_ZombieSkill_Berserk` | 主动 | 短时间进入狂暴状态，提升移速并调整 FOV | `configs/plugins/HZP_ZombieSkill_Berserk/HZPZombieSkillBerserk.jsonc` |
| `HZP_ZombieSkill_Hiding` | 主动 | 进入隐身状态，通过透明度降低实现潜行效果 | `configs/plugins/HZP_ZombieSkill_Hiding/HZPZombieSkillHiding.jsonc` |
| `HZP_ZombieSkill_HealingAura` | 主动 | 以自身为中心治疗附近丧尸，并显示治疗范围/特效 | `configs/plugins/HZP_ZombieSkill_HealingAura/HZPZombieSkillHealingAura.jsonc` |
| `HZP_ZombieSkill_DamageReduction` | 主动 | 开启后降低来自人类的伤害，适合坦克型丧尸 | `configs/plugins/HZP_ZombieSkill_DamageReduction/HZPZombieSkillDamageReduction.jsonc` |
| `HZP_ZombieSkill_Pounce` | 主动 | 猎手式前扑跳跃，向前爆发突进 | `configs/plugins/HZP_ZombieSkill_Pounce/HZPZombieSkillPounce.jsonc` |
| `HZP_ZombieSkill_TongueGrab` | 主动 | 发射舌头钩住正前方人类并拖拽到自己面前 | `configs/plugins/HZP_ZombieSkill_TongueGrab/HZPZombieSkillTongueGrab.jsonc` |
| `HZP_ZombieSkill_DisarmGrenade` | 主动 | 给予一枚自定义诱饵弹，命中人类后强制掉落主武器 | `configs/plugins/HZP_ZombieSkill_DisarmGrenade/HZPZombieSkillDisarmGrenade.jsonc` |
| `HZP_ZombieSkill_ShockwaveGrenade` | 主动 | 给予一枚冲击波手雷，对附近人类造成震屏与击退 | `configs/plugins/HZP_ZombieSkill_ShockwaveGrenade/HZPZombieSkillShockwaveGrenade.jsonc` |
| `HZP_ZombieSkill_DeathExplosion` | 被动 | 死亡时自动爆炸，对周围人类造成范围伤害 | `configs/plugins/HZP_ZombieSkill_DeathExplosion/HZPZombieSkillDeathExplosion.jsonc` |

## 技能触发说明

- 除 `Death Explosion` 外，本仓库中的主动技能默认都监听 `R` 键
- `Healing Aura`、`Berserk`、`Hiding`、`Damage Reduction`、`Pounce`、`Tongue Grab` 会在按下 `R` 后直接生效
- `Disarm Grenade` 与 `Shockwave Grenade` 会在按下 `R` 后给予特殊投掷物，再通过投掷触发效果
- `Death Explosion` 是被动技能，不需要主动释放；按 `R` 仅用于提示说明

## 配置规则

每个插件都有自己的根节点配置，例如：

- `HZPZombieSkillBerserkCFG`
- `HZPZombieSkillPounceCFG`
- `HZPZombieSkillTongueGrabCFG`

这些配置普遍遵循下面的规则：

1. `Groups` 数组中的每一项，代表一个可以使用该技能的丧尸职业配置。
2. `Name` 必须和 `HanZombiePlagueS2` 中返回的丧尸职业名完全一致，否则插件不会生效。
3. `Enable` 用于单独开关该职业的技能。
4. `DeathRefresh` 控制玩家死亡后是否刷新冷却或重置状态。
5. 不同技能会额外提供自己的专属参数，例如：
   - `Berserk` 的 `SpeedMultiplier`
   - `Hiding` 的 `Alpha`
   - `HealingAura` 的 `HealAmount`、`Radius`
   - `Pounce` 的 `ForwardForce`、`VerticalForce`
   - `TongueGrab` 的 `MaxGrabDistance`、`PullSpeed`
   - `ShockwaveGrenade` 的 `ExplosionRadius`、`KnockbackHorizontal`
   - `DeathExplosion` 的 `Damage`、`Radius`

如果你要把技能绑定到新的丧尸职业，最关键的是先保证 `Groups.Name` 和主插件里的职业名称一字不差。

## 安装方式

1. 先确保服务器已经安装并正常运行 `SwiftlyS2`。
2. 再确保主玩法插件 `HanZombiePlagueS2` 已经加载，并且能提供共享接口 `HanZombiePlague`。
3. 按需编译你要使用的技能插件。
4. 将生成的插件文件部署到 `addons/swiftlys2/plugins/`。
5. 将对应配置文件放到 `configs/plugins/<插件目录>/`。
6. 重启服务器或热重载插件后测试技能是否能正常触发。

说明：

- 这个仓库是“技能包”，不是强制全装的单体插件
- 你可以只部署其中 1 个，也可以按职业玩法组合部署多个

## 目录结构

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
   └─ <每个技能对应一份示例配置>
```

## 开发与编译

当前各插件项目都以 `net10.0` 为目标框架，并依赖：

- `SwiftlyS2.CS2`
- `src/api/HanZombiePlagueAPI.dll`

按需编译某个技能项目即可，例如：

```powershell
dotnet build src/HZP_ZombieSkill_TongueGrab/HZP_ZombieSkill_TongueGrab.csproj
```

如果你只想部署部分技能，直接编译并复制对应项目即可，不需要整仓库全部一起上服务器。

## 依赖说明

- 必需依赖：`HanZombiePlagueS2`
- 共享接口名：`HanZombiePlague`
- 运行环境：`SwiftlyS2`
- 当前源码目标框架：`net10.0`

如果主插件没有正确暴露 `HanZombiePlague` 共享接口，这些技能插件会在加载时直接报缺少依赖。

## 适合继续扩展的方向

- 给更多职业补独有技能，并沿用同样的 `Groups.Name` 绑定方式
- 为现有技能补充更多粒子、音效或命中特效
- 增加管理命令、调试日志或更细的权限控制
- 为不同模式做单独的技能参数配置


