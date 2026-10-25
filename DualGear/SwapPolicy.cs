namespace DualGear
{
    internal static class SwapPolicy
    {
        internal static bool ShouldQueue(bool activated, bool hasReserve, bool pending)
            => activated && hasReserve && !pending;

        internal static bool CanCommit(bool sameRun, bool activeUnchanged, bool reservePresent)
            => sameRun && activeUnchanged && reservePresent;
    }
}
