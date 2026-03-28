using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HZP_ZombieSkill;

[PluginMetadata(
    Id = "HZP_ZombieSkill_TongueGrab",
    Version = "1.0.0",
    Name = "HZP Zombie Skill Tongue Grab",
    Author = "H-AN",
    Description = "Zombie skill that hooks a human in front with a laser tongue and drags them to the zombie.")]
public partial class HZP_ZombieSkill_TongueGrab(ISwiftlyCore core) : BasePlugin(core)
{
    private const string HanZombiePlagueKey = "HanZombiePlague";

    private ServiceProvider? _serviceProvider;
    private HZP_ZombieSkill_TongueGrab_Service? _service;

    public static IHanZombiePlagueAPI? ZpApi { get; private set; }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(HanZombiePlagueKey))
        {
            throw new Exception($"[HZP_ZombieSkill_TongueGrab] Missing dependency: {HanZombiePlagueKey}");
        }

        ZpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>(HanZombiePlagueKey);
        if (ZpApi == null)
        {
            throw new Exception($"[HZP_ZombieSkill_TongueGrab] Failed to load {HanZombiePlagueKey} API");
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<HZP_ZombieSkill_TongueGrab_Config>(
                "HZPZombieSkillTongueGrab.jsonc",
                "HZPZombieSkillTongueGrabCFG")
            .Configure(builder =>
            {
                builder.AddJsonFile("HZPZombieSkillTongueGrab.jsonc", false, true);
            });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HZP_ZombieSkill_TongueGrab_Config>()
            .BindConfiguration("HZPZombieSkillTongueGrabCFG");

        collection.AddScoped<HZP_ZombieSkill_TongueGrab_Service>();
        collection.AddScoped<HZP_ZombieSkill_TongueGrab_Helpers>();
        collection.AddScoped<HZP_ZombieSkill_TongueGrab_Globals>();

        _serviceProvider = collection.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<HZP_ZombieSkill_TongueGrab_Service>();
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
