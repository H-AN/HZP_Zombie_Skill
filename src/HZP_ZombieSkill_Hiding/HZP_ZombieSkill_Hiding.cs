using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HZP_ZombieSkill;

[PluginMetadata(Id = "HZP_ZombieSkill_Hiding",
    Version = "1.0.0",
    Name = "僵尸瘟疫,僵尸技能,隐身/HZP,ZombieSkill,Hiding",
    Author = "H-AN",
    Description = "僵尸瘟疫僵尸技能隐身,让僵尸按下技能键后获得隐身能力/HZP,ZombieSkill,Hiding")]

    public partial class HZP_ZombieSkill_Hiding(ISwiftlyCore core) : BasePlugin(core)
    {

        private ServiceProvider? ServiceProvider { get; set; }

        public static IHanZombiePlagueAPI? _zpApi { get; private set; }
        private const string HanZombiePlagueKey = "HanZombiePlague";

        public override void UseSharedInterface(IInterfaceManager interfaceManager)
        {
            if (!interfaceManager.HasSharedInterface(HanZombiePlagueKey))
            {
                throw new Exception($"[HZP_ZombieSkill] 缺少依赖 {HanZombiePlagueKey} / Missing dependency: {HanZombiePlagueKey}");
            }
            _zpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>(HanZombiePlagueKey);
            if (_zpApi == null)
            {
                throw new Exception($"[HZP_ZombieSkill] 读取 {HanZombiePlagueKey} API 失败 / Failed to load {HanZombiePlagueKey} API");
            }
        }

        public override void Load(bool hotReload)
        {
            Core.Configuration.InitializeJsonWithModel<HZP_ZombieSkill_Hiding_Config>("HZPZombieSkillHiding.jsonc", "HZPZombieSkillHidingCFG").Configure(builder =>
            {
                builder.AddJsonFile("HZPZombieSkillHiding.jsonc", false, true);
            });

            var collection = new ServiceCollection();
            collection.AddSwiftly(Core);

            collection
            .AddOptionsWithValidateOnStart<HZP_ZombieSkill_Hiding_Config>()
            .BindConfiguration("HZPZombieSkillHidingCFG");

            collection.AddScoped<HZP_ZombieSkill_Hiding_Service>();
            collection.AddScoped<HZP_ZombieSkill_Hiding_Helpers>();
            collection.AddScoped<HZP_ZombieSkill_Hiding_Globals>();

            ServiceProvider = collection.BuildServiceProvider();

            var service = ServiceProvider.GetRequiredService<HZP_ZombieSkill_Hiding_Service>();
            service.HookEvent();
        }

        public override void Unload()
        {
            ServiceProvider!.Dispose();
        }
    }
