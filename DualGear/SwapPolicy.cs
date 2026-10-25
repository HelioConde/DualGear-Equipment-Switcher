namespace DualGear
{
    internal static class SwapPolicy
    {
        internal static bool ShouldQueue(bool activated, bool hasReserve, bool pending)
            => activated && hasReserve && !pending;

        internal static bool CanCommit(bool sameRun, bool activeUnchanged, bool reservePresent)
            => sameRun && activeUnchanged && reservePresent;
        // Conservative capture: do not interfere with existing reserved equipment,
        // pending swaps, MUL-T or any inventory with multiple equipment sets.
        internal static bool CanCapture(bool hasReserve, bool pending, bool singleLocation)
            => !hasReserve && !pending && singleLocation;
    }
}
