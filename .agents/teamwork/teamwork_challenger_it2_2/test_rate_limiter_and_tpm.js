/**
 * Empirical Test Harness 1: Rate Limiter, Groq TPM Guardrails & 25s Timeout Budget
 * Faithfully implements the exact Python async logic from PRODUCTION_ROADMAP.md §5.5
 */

class AsyncMutex {
    constructor() {
        this._queue = [];
        this._locked = false;
    }

    async acquire() {
        if (!this._locked) {
            this._locked = true;
            return;
        }
        await new Promise(resolve => this._queue.push(resolve));
    }

    release() {
        if (this._queue.length > 0) {
            const next = this._queue.shift();
            next();
        } else {
            this._locked = false;
        }
    }
}

class SlidingWindowRateLimiter {
    constructor(maxRequests = 15, windowSeconds = 60.0, minInterval = 3.8, clock) {
        this.maxRequests = maxRequests;
        this.windowSeconds = windowSeconds;
        this.minInterval = minInterval;
        this.timestamps = [];
        this.lastCallTime = 0.0;
        this.mutex = new AsyncMutex();
        this.clock = clock;
    }

    async acquireSlot(timeoutBudget) {
        await this.mutex.acquire();
        try {
            let now = this.clock.now();

            // 1. Prune timestamps outside rolling 60s window
            while (this.timestamps.length > 0 && (now - this.timestamps[0]) >= this.windowSeconds) {
                this.timestamps.shift();
            }

            // 2. Check if window is full
            let waitTime = 0.0;
            if (this.timestamps.length >= this.maxRequests) {
                const oldest = this.timestamps[0];
                waitTime = Math.max(waitTime, this.windowSeconds - (now - oldest) + 0.1);
            }

            // 3. Check minimum pacing interval
            const timeSinceLast = now - this.lastCallTime;
            if (timeSinceLast < this.minInterval) {
                waitTime = Math.max(waitTime, this.minInterval - timeSinceLast);
            }

            // 4. Evaluate against remaining client budget
            if (waitTime > timeoutBudget) {
                return { acquired: false, waitTime: 0.0 };
            }

            if (waitTime > 0.0) {
                await this.clock.sleep(waitTime);
                now = this.clock.now();
            }

            // 5. Record dispatch
            this.timestamps.push(now);
            this.lastCallTime = now;
            return { acquired: true, waitTime };
        } finally {
            this.mutex.release();
        }
    }
}

class GroqTokenTracker {
    constructor(tpmLimit = 6000, safetyMargin = 800, clock) {
        this.tpmLimit = tpmLimit;
        this.safetyCeiling = tpmLimit - safetyMargin; // 5,200 tokens
        this.tokenHistory = [];
        this.mutex = new AsyncMutex();
        this.clock = clock;
    }

    async canConsume(estimatedTokens) {
        await this.mutex.acquire();
        try {
            const now = this.clock.now();
            while (this.tokenHistory.length > 0 && (now - this.tokenHistory[0].time) >= 60.0) {
                this.tokenHistory.shift();
            }

            const currentTpm = this.tokenHistory.reduce((sum, item) => sum + item.tokens, 0);
            if (currentTpm + estimatedTokens > this.safetyCeiling) {
                return false;
            }
            return true;
        } finally {
            this.mutex.release();
        }
    }

    async recordConsumption(actualTokens) {
        await this.mutex.acquire();
        try {
            this.tokenHistory.push({ time: this.clock.now(), tokens: actualTokens });
        } finally {
            this.mutex.release();
        }
    }
}

// Virtual Discrete-Event Simulation Clock
class VirtualClock {
    constructor() {
        this.currentTime = 1000.0; // Start at t=1000s
        this.eventQueue = []; // { time, resolve }
    }

    now() {
        return this.currentTime;
    }

    async sleep(seconds) {
        const targetTime = this.currentTime + seconds;
        return new Promise(resolve => {
            this.eventQueue.push({ time: targetTime, resolve });
            this.eventQueue.sort((a, b) => a.time - b.time);
            this.step();
        });
    }

    step() {
        // If there are events and microtasks are settled, advance to next event
        setImmediate(() => {
            if (this.eventQueue.length > 0) {
                // Find next batch of events at the earliest time
                const nextEvent = this.eventQueue.shift();
                if (nextEvent.time > this.currentTime) {
                    this.currentTime = nextEvent.time;
                }
                nextEvent.resolve();
            }
        });
    }
}

// Resilient Provider Factory Simulator
class ResilientFactorySimulator {
    constructor(clock, limiter, tracker) {
        this.clock = clock;
        this.limiter = limiter;
        this.tracker = tracker;
        this.geminiDispatches = [];
        this.groqDispatches = [];
        this.fallbackDispatches = [];
    }

    async generate(reqId, totalBudgetSec = 25.0) {
        const startTime = this.clock.now();
        const deadline = startTime + totalBudgetSec;
        const promptTokenEst = 1200;

        // 1. Primary: Gemini
        const maxGeminiAttempts = 2;
        for (let attempt = 0; attempt < maxGeminiAttempts; attempt++) {
            let timeRemaining = deadline - this.clock.now();
            if (timeRemaining < 6.0) break;

            const { acquired, waitTime } = await this.limiter.acquireSlot(timeRemaining - 4.0);
            if (!acquired) break;

            timeRemaining = deadline - this.clock.now();
            const perCallTimeout = Math.min(8.0, Math.max(2.0, timeRemaining - 1.0));

            // Simulate Gemini call (0.8s)
            await this.clock.sleep(0.8);
            const now = this.clock.now();
            this.geminiDispatches.push({ time: now, reqId, attempt });
            return {
                reqId,
                provider: "gemini",
                elapsed: now - startTime
            };
        }

        // 2. Failover: Groq
        let timeRemaining = deadline - this.clock.now();
        if (timeRemaining >= 4.0) {
            const failoverMaxTokens = Math.min(384, 1024);
            const estimatedReqTokens = promptTokenEst + failoverMaxTokens;

            if (await this.tracker.canConsume(estimatedReqTokens)) {
                const groqTimeout = Math.min(6.0, timeRemaining - 0.5);
                await this.clock.sleep(0.5); // Groq call
                const now = this.clock.now();
                await this.tracker.recordConsumption(estimatedReqTokens);
                this.groqDispatches.push({ time: now, reqId, tokens: estimatedReqTokens });
                return {
                    reqId,
                    provider: "groq",
                    elapsed: now - startTime
                };
            }
        }

        // 3. Fallback: Local Deterministic
        const now = this.clock.now();
        this.fallbackDispatches.push({ time: now, reqId });
        return {
            reqId,
            provider: "local_deterministic_fallback",
            elapsed: now - startTime
        };
    }
}

async function runTest() {
    console.log("=== EMPIRICAL STRESS TEST: 25-STUDENT CONCURRENT BURST ===");
    const clock = new VirtualClock();
    const limiter = new SlidingWindowRateLimiter(15, 60.0, 3.8, clock);
    const tracker = new GroqTokenTracker(6000, 800, clock);
    const factory = new ResilientFactorySimulator(clock, limiter, tracker);

    const startTime = clock.now();
    const promises = [];
    for (let i = 0; i < 25; i++) {
        promises.push(factory.generate(i, 25.0));
    }

    const results = await Promise.all(promises);
    const totalElapsed = clock.now() - startTime;

    console.log(`\n1. Burst Execution Metrics:`);
    console.log(`- Total simulation duration: ${totalElapsed.toFixed(2)}s`);
    
    const counts = { gemini: 0, groq: 0, local_deterministic_fallback: 0 };
    const latencies = [];
    for (const r of results) {
        counts[r.provider] = (counts[r.provider] || 0) + 1;
        latencies.push(r.elapsed);
    }
    console.log(`- Request Distribution:`, counts);
    console.log(`- Min Latency: ${Math.min(...latencies).toFixed(2)}s`);
    console.log(`- Max Latency: ${Math.max(...latencies).toFixed(2)}s`);
    console.log(`- Avg Latency: ${(latencies.reduce((a,b)=>a+b,0)/latencies.length).toFixed(2)}s`);

    console.log(`\n2. Gemini Rate Limiting & Pacing Verification:`);
    console.log(`- Gemini dispatches: ${factory.geminiDispatches.length}`);
    const intervals = [];
    for (let i = 1; i < factory.geminiDispatches.length; i++) {
        intervals.push(factory.geminiDispatches[i].time - factory.geminiDispatches[i-1].time);
    }
    if (intervals.length > 0) {
        console.log(`- Dispatch Intervals: min=${Math.min(...intervals).toFixed(2)}s, max=${Math.max(...intervals).toFixed(2)}s, avg=${(intervals.reduce((a,b)=>a+b,0)/intervals.length).toFixed(2)}s`);
    }

    console.log(`\n3. Groq TPM Guardrail Verification:`);
    const totalGroqTokens = factory.groqDispatches.reduce((sum, d) => sum + d.tokens, 0);
    console.log(`- Groq queries dispatched: ${factory.groqDispatches.length}`);
    console.log(`- Groq tokens consumed: ${totalGroqTokens} / 5,200 ceiling (6,000 hard limit)`);

    console.log(`\n4. Cloudflare 100s & ASP.NET 30s Timeout Verification:`);
    console.log(`- Max latency across all 25 requests: ${Math.max(...latencies).toFixed(2)}s`);
    console.log(`- Margin below ASP.NET 30s timeout: ${(30.0 - Math.max(...latencies)).toFixed(2)}s`);
    console.log(`- Margin below Cloudflare 100s timeout: ${(100.0 - Math.max(...latencies)).toFixed(2)}s`);

    // Verification Assertions
    let passed = true;
    if (factory.geminiDispatches.length > 15) {
        console.error("FAIL: Gemini dispatches exceeded 15!");
        passed = false;
    }
    if (intervals.some(inv => inv < 3.79)) {
        console.error("FAIL: Gemini pacing interval breached minimum 3.8s!");
        passed = false;
    }
    if (totalGroqTokens > 5200) {
        console.error("FAIL: Groq token consumption exceeded safety ceiling of 5,200!");
        passed = false;
    }
    if (Math.max(...latencies) > 30.0) {
        console.error("FAIL: Request latency breached ASP.NET 30.0s client timeout!");
        passed = false;
    }
    if (Math.max(...latencies) > 100.0) {
        console.error("FAIL: Request latency breached Cloudflare 100.0s proxy timeout!");
        passed = false;
    }

    if (passed) {
        console.log("\n>>> ALL STRESS TEST ASSERTIONS EMPIRICALLY CONFIRMED AND PASSED! <<<");
    } else {
        process.exit(1);
    }
}

runTest().catch(err => {
    console.error("Fatal error:", err);
    process.exit(1);
});
