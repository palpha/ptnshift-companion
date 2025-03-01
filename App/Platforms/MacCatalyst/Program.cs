using System.Runtime.InteropServices;
using UIKit;

namespace LiveshiftCompanion;

public class Program
{
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.WriteLine("Unhandled Exception: " + e.ExceptionObject);
        };

        try
        {
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}