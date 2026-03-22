using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace HZP_ZombieSkill;

public class HZP_ZombieSkill_Berserk_Helpers
{
    private readonly ILogger<HZP_ZombieSkill_Berserk_Helpers> _logger;
    private readonly ISwiftlyCore _core;

    public HZP_ZombieSkill_Berserk_Helpers(ISwiftlyCore core, ILogger<HZP_ZombieSkill_Berserk_Helpers> logger)
    {
        _core = core;
        _logger = logger;
    }
}
