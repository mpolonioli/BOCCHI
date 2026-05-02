using Ocelot.Config;
using Ocelot.Config.Fields;

namespace BOCCHI.Common.Config;

[Serializable]
[ConfigGroup("automation")]
public class CombatConfig : IAutoConfig
{
    [Checkbox] public bool ShouldHandleTargeting { get; set; } = false;
}
