using System;

// Runs the same Editor validation against Unity's managed types without launching
// the Editor. No gameplay scene, saved progress or Firebase connection is touched.
internal static class ArenaReachabilityCheck
{
    private static int Main()
    {
        try
        {
            Console.WriteLine($"Arena reachability: {ArenaReachabilityValidation.Validate()} checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
