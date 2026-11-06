using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== EMPIRICAL TEST: Timing Attack Resistance of string.Equals vs Constant-Time ===");
        
        string targetToken = "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0"; // 64 chars
        string mismatchFirstChar = "x1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0";
        string mismatchLastChar  = "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdefx";
        
        const int iterations = 10_000_000;
        
        // Warm up JIT
        for (int i = 0; i < 10000; i++)
        {
            string.Equals(targetToken, mismatchFirstChar, StringComparison.Ordinal);
            string.Equals(targetToken, mismatchLastChar, StringComparison.Ordinal);
        }

        // Test string.Equals with early mismatch
        var sw1 = Stopwatch.StartNew();
        int sum1 = 0;
        for (int i = 0; i < iterations; i++)
        {
            if (string.Equals(targetToken, mismatchFirstChar, StringComparison.Ordinal)) sum1++;
        }
        sw1.Stop();

        // Test string.Equals with late mismatch
        var sw2 = Stopwatch.StartNew();
        int sum2 = 0;
        for (int i = 0; i < iterations; i++)
        {
            if (string.Equals(targetToken, mismatchLastChar, StringComparison.Ordinal)) sum2++;
        }
        sw2.Stop();

        Console.WriteLine($"string.Equals (Early Mismatch): {sw1.ElapsedMilliseconds} ms (ticks: {sw1.ElapsedTicks})");
        Console.WriteLine($"string.Equals (Late Mismatch):  {sw2.ElapsedMilliseconds} ms (ticks: {sw2.ElapsedTicks})");
        double diffPct = ((double)(sw2.ElapsedTicks - sw1.ElapsedTicks) / sw1.ElapsedTicks) * 100.0;
        Console.WriteLine($"Timing Delta: {diffPct:F2}% difference between early and late mismatch!");

        // Test null safety
        Console.WriteLine("\n--- Testing Null Handling ---");
        try
        {
            string nullToken = null;
            bool eq = string.Equals(nullToken, "submitted", StringComparison.Ordinal);
            Console.WriteLine($"string.Equals(null, submitted): {eq} (Did not throw)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Threw: {ex.GetType().Name}: {ex.Message}");
        }

        // Now test FixedTimeEquals
        byte[] targetBytes = Encoding.UTF8.GetBytes(targetToken);
        byte[] firstMismatchBytes = Encoding.UTF8.GetBytes(mismatchFirstChar);
        byte[] lastMismatchBytes = Encoding.UTF8.GetBytes(mismatchLastChar);

        var sw3 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            CryptographicOperations.FixedTimeEquals(targetBytes, firstMismatchBytes);
        }
        sw3.Stop();

        var sw4 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            CryptographicOperations.FixedTimeEquals(targetBytes, lastMismatchBytes);
        }
        sw4.Stop();

        Console.WriteLine($"\nCryptographicOperations.FixedTimeEquals (Early): {sw3.ElapsedMilliseconds} ms (ticks: {sw3.ElapsedTicks})");
        Console.WriteLine($"CryptographicOperations.FixedTimeEquals (Late):  {sw4.ElapsedMilliseconds} ms (ticks: {sw4.ElapsedTicks})");
    }
}
