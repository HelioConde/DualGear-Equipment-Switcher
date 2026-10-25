using System;
using DualGear;
internal static class Program
{
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAILED: " + name);
    }
    private static int Main()
    {
        try
        {
            Check(SwapPolicy.ShouldQueue(true, true, false), "activated");
            Check(!SwapPolicy.ShouldQueue(false, true, false), "activation failure");
            Check(!SwapPolicy.ShouldQueue(true, false, false), "empty reserve");
            Check(!SwapPolicy.ShouldQueue(true, true, true), "pending");
            Check(SwapPolicy.CanCommit(true, true, true), "commit");
            Check(!SwapPolicy.CanCommit(false, true, true), "old run");
            Check(!SwapPolicy.CanCommit(true, false, true), "pickup race");
            Check(!SwapPolicy.CanCommit(true, true, false), "reserve missing");
            Console.WriteLine("PASS: 8 DualGear policy tests.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
