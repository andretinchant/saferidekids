using ObjCRuntime;
using UIKit;

namespace SafeRideKids.Familia.Maui;

public class Program
{
    // Ponto de entrada do app iOS — apenas chama UIApplication.Main com o AppDelegate.
    static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
