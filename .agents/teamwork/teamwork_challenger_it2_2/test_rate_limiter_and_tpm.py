"""
Empirical Test Harness 1: Rate Limiter, Groq TPM Guardrails & 25s Timeout Budget
Tests the exact logic from PRODUCTION_ROADMAP.md Section 5.5
"""
import time
import asyncio
from collections import deque
from typing import Tuple, List, Dict, Any

class SlidingWindowRateLimiter:
    def __init__(self, max_requests: int = 15, window_seconds: float = 60.0, min_interval: float = 3.8):
        self.max_requests = max_requests
        self.window_seconds = window_seconds
        self.min_interval = min_interval
        self.timestamps: deque = deque()
        self.last_call_time: float = 0.0
        self._lock = asyncio.Lock()

    async def acquire_slot(self, timeout_budget: float) -> Tuple[bool, float]:
        async with self._lock:
            now = time.monotonic()
            
            # 1. Prune timestamps outside rolling 60s window
            while self.timestamps and (now - self.timestamps[0]) >= self.window_seconds:
                self.timestamps.popleft()

            # 2. Check if window is full
            wait_time = 0.0
            if len(self.timestamps) >= self.max_requests:
                oldest_timestamp = self.timestamps[0]
                wait_time = max(wait_time, self.window_seconds - (now - oldest_timestamp) + 0.1)

            # 3. Check minimum pacing interval
            time_since_last = now - self.last_call_time
            if time_since_last < self.min_interval:
                wait_time = max(wait_time, self.min_interval - time_since_last)

            # 4. Evaluate against remaining client budget
            if wait_time > timeout_budget:
                return False, 0.0

            if wait_time > 0.0:
                await asyncio.sleep(wait_time)
                now = time.monotonic()

            # 5. Record dispatch
            self.timestamps.append(now)
            self.last_call_time = now
            return True, wait_time


class GroqTokenTracker:
    def __init__(self, tpm_limit: int = 6000, safety_margin: int = 800):
        self.tpm_limit = tpm_limit
        self.safety_ceiling = tpm_limit - safety_margin  # 5,200 tokens
        self.token_history: deque = deque()
        self._lock = asyncio.Lock()

    async def can_consume(self, estimated_tokens: int) -> bool:
        async with self._lock:
            now = time.monotonic()
            while self.token_history and (now - self.token_history[0][0]) >= 60.0:
                self.token_history.popleft()

            current_tpm = sum(tokens for _, tokens in self.token_history)
            if current_tpm + estimated_tokens > self.safety_ceiling:
                return False
            return True

    async def record_consumption(self, actual_tokens: int):
        async with self._lock:
            self.token_history.append((time.monotonic(), actual_tokens))


# Mocked simulation of ResilientLLMProviderFactory
class MockResilientFactory:
    def __init__(self, gemini_limiter: SlidingWindowRateLimiter, groq_tracker: GroqTokenTracker):
        self.gemini_limiter = gemini_limiter
        self.groq_tracker = groq_tracker
        self.gemini_dispatches = []
        self.groq_dispatches = []
        self.fallback_dispatches = []

    async def generate(self, req_id: int, total_budget_sec: float = 25.0) -> Dict[str, Any]:
        start_time = time.monotonic()
        deadline = start_time + total_budget_sec
        prompt_token_est = 1200 # typical RAG prompt tokens

        # 1. Primary: Gemini
        max_gemini_attempts = 2
        for attempt in range(max_gemini_attempts):
            time_remaining = deadline - time.monotonic()
            if time_remaining < 6.0:
                break

            acquired, wait_spent = await self.gemini_limiter.acquire_slot(timeout_budget=time_remaining - 4.0)
            if not acquired:
                break

            time_remaining = deadline - time.monotonic()
            per_call_timeout = min(8.0, max(2.0, time_remaining - 1.0))

            try:
                # Simulate Gemini API latency (e.g. 0.8s)
                call_duration = min(0.8, per_call_timeout)
                await asyncio.sleep(call_duration)
                now = time.monotonic()
                self.gemini_dispatches.append((now, req_id))
                return {
                    "req_id": req_id,
                    "provider": "gemini",
                    "elapsed": now - start_time
                }
            except Exception:
                break

        # 2. Failover: Groq
        time_remaining = deadline - time.monotonic()
        if time_remaining >= 4.0:
            failover_max_tokens = 384
            estimated_req_tokens = prompt_token_est + failover_max_tokens

            if await self.groq_tracker.can_consume(estimated_req_tokens):
                groq_timeout = min(6.0, time_remaining - 0.5)
                # Simulate Groq API latency (e.g. 0.5s)
                await asyncio.sleep(min(0.5, groq_timeout))
                now = time.monotonic()
                actual_tokens = estimated_req_tokens
                await self.groq_tracker.record_consumption(actual_tokens)
                self.groq_dispatches.append((now, req_id, actual_tokens))
                return {
                    "req_id": req_id,
                    "provider": "groq",
                    "elapsed": now - start_time
                }

        # 3. Deterministic Local Fallback
        now = time.monotonic()
        self.fallback_dispatches.append((now, req_id))
        return {
            "req_id": req_id,
            "provider": "local_fallback",
            "elapsed": now - start_time
        }


async def run_stress_test():
    gemini_limiter = SlidingWindowRateLimiter(max_requests=15, window_seconds=60.0, min_interval=3.8)
    groq_tracker = GroqTokenTracker(tpm_limit=6000, safety_margin=800)
    factory = MockResilientFactory(gemini_limiter, groq_tracker)

    print("=== STRESS TEST: 25 SIMULTANEOUS STUDENT REQUESTS ===")
    t0 = time.monotonic()
    tasks = [factory.generate(i, total_budget_sec=25.0) for i in range(25)]
    results = await asyncio.gather(*tasks)
    total_duration = time.monotonic() - t0

    print(f"Total burst processing completed in {total_duration:.2f}s")
    
    provider_counts = {}
    latencies = []
    for r in results:
        p = r["provider"]
        provider_counts[p] = provider_counts.get(p, 0) + 1
        latencies.append(r["elapsed"])

    print(f"Provider breakdown: {provider_counts}")
    print(f"Max request latency: {max(latencies):.2f}s, Min: {min(latencies):.2f}s, Avg: {sum(latencies)/len(latencies):.2f}s")

    # Check Gemini rate limit compliance
    gemini_times = [t for t, _ in factory.gemini_dispatches]
    print(f"Total Gemini dispatches: {len(gemini_times)}")
    intervals = [gemini_times[i] - gemini_times[i-1] for i in range(1, len(gemini_times))]
    if intervals:
        print(f"Gemini dispatch intervals: min={min(intervals):.2f}s, max={max(intervals):.2f}s, avg={sum(intervals)/len(intervals):.2f}s")
        assert min(intervals) >= 3.79, f"Violation: interval {min(intervals):.2f}s < 3.8s!"
    
    # Check max requests in any 60s window
    assert len(gemini_times) <= 15, f"Violation: Gemini received {len(gemini_times)} > 15 requests!"

    # Check Groq TPM compliance
    groq_tokens = sum(tokens for _, _, tokens in factory.groq_dispatches)
    print(f"Total Groq dispatches: {len(factory.groq_dispatches)}, Total Groq tokens consumed: {groq_tokens}")
    assert groq_tokens <= 5200, f"Violation: Groq consumed {groq_tokens} > 5200 tokens!"

    # Check Cloudflare and ASP.NET timeout compliance
    max_lat = max(latencies)
    print(f"Max latency vs Cloudflare 100s limit: {max_lat:.2f}s (Budget headroom: {100.0 - max_lat:.2f}s)")
    print(f"Max latency vs ASP.NET 30s limit: {max_lat:.2f}s (Budget headroom: {30.0 - max_lat:.2f}s)")
    assert max_lat <= 30.0, f"Violation: Max latency {max_lat:.2f}s exceeded ASP.NET 30s timeout!"
    assert max_lat <= 100.0, f"Violation: Max latency {max_lat:.2f}s exceeded Cloudflare 100s timeout!"

    print("ALL ASSERTIONS PASSED EMPIRICALLY!")

if __name__ == "__main__":
    asyncio.run(run_stress_test())
