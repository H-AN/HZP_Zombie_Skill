using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HZP_ZombieSkill;

[PluginMetadata(
    Id = "HZP_ZombieSkill_ShockwaveGrenade",
    Version = "1.0.0",
    Name = "HZP Zombie Skill Shockwave Grenade",
    Author = "H-AN",
    Description = "Zombie skill that grants a custom decoy grenade which shakes and knocks back nearby humans.")]
public partial class HZP_ZombieSkill_ShockwaveGrenade(ISwiftlyCore core) : BasePlugin(core)
{
    private const string HanZombiePlagueKey = "HanZombiePlague";

    private ServiceProvider? _serviceProvider;
    private HZP_ZombieSkill_ShockwaveGrenade_Service? _service;

    public static IHanZombiePlagueAPI? ZpApi { get; private set; }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (!interfaceManager.HasSharedInterface(HanZombiePlagueKey))
        {
            throw new Exception($"[HZP_ZombieSkill_ShockwaveGrenade] Missing dependency: {HanZombiePlagueKey}");
        }

        ZpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>(HanZombiePlagueKey);
        if (ZpApi == null)
        {
            throw new Exception($"[HZP_ZombieSkill_ShockwaveGrenade] Failed to load {HanZombiePlagueKey} API");
        }
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<HZP_ZombieSkill_ShockwaveGrenade_Config>(
                "HZPZombieSkillShockwaveGrenade.jsonc",
                "HZPZombieSkillShockwaveGrenadeCFG")
            .Configure(builder =>
            {
                builder.AddJsonFile("HZPZombieSkillShockwaveGrenade.jsonc", false, true);
            });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HZP_ZombieSkill_ShockwaveGrenade_Config>()
            .BindConfiguration("HZPZombieSkillShockwaveGrenadeCFG");

        collection.AddScoped<HZP_ZombieSkill_ShockwaveGrenade_Service>();
        collection.AddScoped<HZP_ZombieSkill_ShockwaveGrenade_Helpers>();
        collection.AddScoped<HZP_ZombieSkill_ShockwaveGrenade_Globals>();

        _serviceProvider = collection.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<HZP_ZombieSkill_ShockwaveGrenade_Service>();
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
