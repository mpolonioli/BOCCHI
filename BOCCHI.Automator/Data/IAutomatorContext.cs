using BOCCHI.Automator.Data.Goals;

namespace BOCCHI.Automator.Data;

public interface IAutomatorContext
{
    bool Enabled { get; }

    void Toggle();

    event Action<bool>? OnToggle;
}
