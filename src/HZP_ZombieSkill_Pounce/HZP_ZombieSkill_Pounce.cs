using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HZP_ZombieSkill;

[PluginMetadata(
    Id = "HZP_ZombieSkill_Pounce",
    Version = "1.0.0",
    Name = "HZP Zombie Skill Pounce",
    Author = "H-AN",
    Description = "Hunter-style zombie pounce that launches the player forward.")]
public partial class HZP_ZombieSkill_Pounce(ISwiftlyCore core) : BasePlugin(core)
{
    private const string HanZombiePlagueKey = "HanZombiePlague";

    private ServiceProvider? _serviceProvider;
    private HZP_ZombieSkill_Pounce_Service? _service;

    public static IHanZombiePlagueAPI? ZpApi { get; private set; }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(HanZombiePlagueKey))
        {
            throw new Exception($"[HZP_ZombieSkill_Pounce] Missing dependency: {HanZombiePlagueKey}");
        }

        ZpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>(HanZombiePlagueKey);
        if (ZpApi == null)
        {
            throw new Exception($"[HZP_ZombieSkill_Pounce] Failed to load {HanZombiePlagueKey} API");
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<HZP_ZombieSkill_Pounce_Config>(
                "HZPZombieSkillPounce.jsonc",
                "HZPZombieSkillPounceCFG")
            .Configure(builder =>
            {
                builder.AddJsonFile("HZPZombieSkillPounce.jsonc", false, true);
            });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HZP_ZombieSkill_Pounce_Config>()
            .BindConfiguration("HZPZombieSkillPounceCFG");

        collection.AddScoped<HZP_ZombieSkill_Pounce_Service>();
        collection.AddScoped<HZP_ZombieSkill_Pounce_Helpers>();
        collection.AddScoped<HZP_ZombieSkill_Pounce_Globals>();

        _serviceProvider = collection.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<HZP_ZombieSkill_Pounce_Service>();
        _service.HookEvent();
    }

    public override void Unload()
    {
        _service?.CleanupOnUnload();
        _serviceProvider?.Dispose();

        _service = null;
        _serviceProvider = null;
    }
}
