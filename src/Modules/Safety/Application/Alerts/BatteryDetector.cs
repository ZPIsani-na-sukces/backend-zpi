using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Alerts;

internal static class BatteryDetector
{
    public static bool Evaluate(AutomaticAlertState state, BatterySample sample, DateTimeOffset now, AlertOptions options)
    {
        if (!options.IsFresh(sample.CapturedAt, now) || sample.CapturedAt <= state.BatteryEvaluatedAt) return false;
        state.BatteryEvaluatedAt = sample.CapturedAt;
        if (sample.IsCharging) state.BatteryNotificationActive = false;
        if (sample.BatteryLevelPercent >= options.BatteryRearmPercent)
        {
            state.BatteryArmed = true;
            state.BatteryNotificationActive = false;
            return false;
        }
        if (!state.BatteryArmed || sample.IsCharging || sample.BatteryLevelPercent > options.LowBatteryPercent) return false;
        state.BatteryArmed = false;
        return true;
    }
}
