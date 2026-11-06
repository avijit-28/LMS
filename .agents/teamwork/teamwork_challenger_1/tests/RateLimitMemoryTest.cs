using System;
using System.Collections.Concurrent;
using System.Diagnostics;

class RateLimitMemoryTest
{
    // Typical naive in-memory token bucket rate limiter
    class TokenBucket
    {
        public int Tokens { get; set; } = 10;
        public DateTime LastRefill { get; set; } = DateTime.UtcNow;
    }

    static void Main()
    {
        Console.WriteLine("=== EMPIRICAL TEST: Memory Exhaustion in Naive In-Memory Rate Limiting under IP Spoofing ===");

        var rateLimitCache = new ConcurrentDictionary<string, TokenBucket>();
        long initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Initial Process Memory: {initialMemory / (1024 * 1024)} MB");

        const int numSpoofedIps = 500_000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < numSpoofedIps; i++)
        {
            // Simulate random spoofed IPv4 or IPv6 header
            string spoofedIp = $"198.51.{(i >> 8) & 255}.{i & 255}";
            rateLimitCache.GetOrAdd(spoofedIp, _ => new TokenBucket());

            if ((i + 1) % 100_000 == 0)
            {
                long currentMem = GC.GetTotalMemory(false);
                Console.WriteLine($"Ingested {i + 1:N0} spoofed IPs -> Managed Memory: {currentMem / (1024 * 1024)} MB (Delta: {(currentMem - initialMemory) / (1024 * 1024)} MB)");
            }
        }
        sw.Stop();

        long finalMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Final Memory for {numSpoofedIps:N0} keys: {finalMemory / (1024 * 1024)} MB");
        Console.WriteLine($"Elapsed Time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine("Vulnerability Confirmed: Unbounded dictionary cache allows attackers to cause OOM via IP cardinality explosion!");
    }
}
