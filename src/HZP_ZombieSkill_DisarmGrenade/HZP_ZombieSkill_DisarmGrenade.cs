using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HZP_ZombieSkill;

[PluginMetadata(
    Id = "HZP_ZombieSkill_DisarmGrenade",
    Version = "1.0.0",
    Name = "HZP Zombie Skill Disarm Grenade",
    Author = "H-AN",
    Description = "Zombie skill that grants a custom decoy grenade which forces humans to drop their primary weapon on hit.")]
public partial class HZP_ZombieSkill_DisarmGrenade(ISwiftlyCore core) : BasePlugin(core)
{
    private const string HanZombiePlagueKey = "HanZombiePlague";

    private ServiceProvider? _serviceProvider;
    private HZP_ZombieSkill_DisarmGrenade_Service? _service;

    public static IHanZombiePlagueAPI? ZpApi { get; private set; }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(HanZombiePlagueKey))
        {
            throw new Exception($"[HZP_ZombieSkill_DisarmGrenade] Missing dependency: {HanZombiePlagueKey}");
        }

        ZpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>(HanZombiePlagueKey);
        if (ZpApi == null)
        {
            throw new Exception($"[HZP_ZombieSkill_DisarmGrenade] Failed to load {HanZombiePlagueKey} API");
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<HZP_ZombieSkill_DisarmGrenade_Config>(
                "HZPZombieSkillDisarmGrenade.jsonc",
                "HZPZombieSkillDisarmGrenadeCFG")
            .Configure(builder =>
            {
                builder.AddJsonFile("HZPZombieSkillDisarmGrenade.jsonc", false, true);
            });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HZP_ZombieSkill_DisarmGrenade_Config>()
            .BindConfiguration("HZPZombieSkillDisarmGrenadeCFG");

        collection.AddScoped<HZP_ZombieSkill_DisarmGrenade_Service>();
        collection.AddScoped<HZP_ZombieSkill_DisarmGrenade_Helpers>();
        collection.AddScoped<HZP_ZombieSkill_DisarmGrenade_Globals>();

        _serviceProvider = collection.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<HZP_ZombieSkill_DisarmGrenade_Service>();
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
