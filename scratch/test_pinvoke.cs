using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("InnoEngine.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Engine_GetRegisteredTotal();

    static void Main() {
        try {
            int total = Engine_GetRegisteredTotal();
            Console.WriteLine("Total from DLL: " + total);
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
