/**
 * Verification of the Stale Timeout Budget / Lock Contention Bug
 */

const { Worker } = require('worker_threads');

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

// Model 1: As written in PRODUCTION_ROADMAP.md (passes static timeout_budget)
class RoadmapLimiter {
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
        // Contention happens here: waiting for mutex
        await this.mutex.acquire();
        try {
            let now = this.clock.now();

            while (this.timestamps.length > 0 && (now - this.timestamps[0]) >= this.windowSeconds) {
                this.timestamps.shift();
            }

            let waitTime = 0.0;
            if (this.timestamps.length >= this.maxRequests) {
                const oldest = this.timestamps[0];
                waitTime = Math.max(waitTime, this.windowSeconds - (now - oldest) + 0.1);
            }

            const timeSinceLast = now - this.lastCallTime;
            if (timeSinceLast < this.minInterval) {
                waitTime = Math.max(waitTime, this.minInterval - timeSinceLast);
            }

            // BUG: timeoutBudget is stale! It was calculated before waiting for mutex.
            if (waitTime > timeoutBudget) {
                return { acquired: false, waitTime: 0.0 };
            }

            if (waitTime > 0.0) {
                await this.clock.sleep(waitTime);
                now = this.clock.now();
            }

            this.timestamps.push(now);
            this.lastCallTime = now;
            return { acquired: true, waitTime };
        } finally {
            this.mutex.release();
        }
    }
}

// Model 2: Corrected Limiter (passes absolute deadline or checks remaining time after lock acquisition)
class FixedLimiter {
    constructor(maxRequests = 15, windowSeconds = 60.0, minInterval = 3.8, clock) {
        this.maxRequests = maxRequests;
        this.windowSeconds = windowSeconds;
        this.minInterval = minInterval;
        this.timestamps = [];
        this.lastCallTime = 0.0;
        this.mutex = new AsyncMutex();
        this.clock = clock;
    }

    async acquireSlot(deadline, minHeadroom = 4.0) {
        await this.mutex.acquire();
        try {
            let now = this.clock.now();

            // Check if deadline already passed while waiting for lock
            if (now >= deadline - minHeadroom) {
                return { acquired: false, waitTime: 0.0 };
            }

            while (this.timestamps.length > 0 && (now - this.timestamps[0]) >= this.windowSeconds) {
                this.timestamps.shift();
            }

            let waitTime = 0.0;
            if (this.timestamps.length >= this.maxRequests) {
                const oldest = this.timestamps[0];
                waitTime = Math.max(waitTime, this.windowSeconds - (now - oldest) + 0.1);
            }

            const timeSinceLast = now - this.lastCallTime;
            if (timeSinceLast < this.minInterval) {
                waitTime = Math.max(waitTime, this.minInterval - timeSinceLast);
            }

            // Evaluate against FRESH remaining budget
            const freshRemaining = deadline - now - minHeadroom;
            if (waitTime > freshRemaining) {
                return { acquired: false, waitTime: 0.0 };
            }

            if (waitTime > 0.0) {
                await this.clock.sleep(waitTime);
                now = this.clock.now();
            }

            this.timestamps.push(now);
            this.lastCallTime = now;
            return { acquired: true, waitTime };
        } finally {
            this.mutex.release();
        }
    }
}

class VirtualClock {
    constructor() {
        this.currentTime = 1000.0;
        this.eventQueue = [];
    }
    now() { return this.currentTime; }
    async sleep(seconds) {
        const target = this.currentTime + seconds;
        return new Promise(resolve => {
            this.eventQueue.push({ time: target, resolve });
            this.eventQueue.sort((a, b) => a.time - b.time);
            this.step();
        });
    }
    step() {
        setImmediate(() => {
            if (this.eventQueue.length > 0) {
                const next = this.eventQueue.shift();
                if (next.time > this.currentTime) {
                    this.currentTime = next.time;
                }
                next.resolve();
            }
        });
    }
}

async function simulateFixed() {
    console.log("\n--- SIMULATION OF FIXED LIMITER (WITH FRESH DEADLINE CHECK) ---");
    const clock = new VirtualClock();
    const limiter = new FixedLimiter(15, 60.0, 3.8, clock);
    
    // Test Groq token tracker
    let groqTokens = 0;
    const results = [];

    const tasks = Array.from({ length: 25 }, async (_, reqId) => {
        const startTime = clock.now();
        const deadline = startTime + 25.0;

        // Try Gemini with fixed limiter
        const { acquired } = await limiter.acquireSlot(deadline, 4.0);
        if (acquired) {
            await clock.sleep(0.8);
            const now = clock.now();
            return { reqId, provider: "gemini", elapsed: now - startTime };
        }

        // Failover to Groq if budget remains
        let remaining = deadline - clock.now();
        if (remaining >= 4.0) {
            const reqTokens = 1200 + 384; // 1584 tokens
            if (groqTokens + reqTokens <= 5200) {
                groqTokens += reqTokens;
                await clock.sleep(0.5);
                const now = clock.now();
                return { reqId, provider: "groq", elapsed: now - startTime };
            }
        }

        // Local Fallback
        const now = clock.now();
        return { reqId, provider: "local_fallback", elapsed: now - startTime };
    });

    const res = await Promise.all(tasks);
    const breakdown = {};
    const latencies = [];
    for (const r of res) {
        breakdown[r.provider] = (breakdown[r.provider] || 0) + 1;
        latencies.push(r.elapsed);
    }
    console.log("Fixed Limiter Results:", breakdown);
    console.log(`Max Latency: ${Math.max(...latencies).toFixed(2)}s, Min: ${Math.min(...latencies).toFixed(2)}s`);
    console.log(`Groq Tokens Consumed: ${groqTokens} / 5,200 ceiling`);
    console.log(`All requests completed within ASP.NET 30s timeout? ${Math.max(...latencies) <= 30.0}`);
    console.log(`All requests completed within Cloudflare 100s timeout? ${Math.max(...latencies) <= 100.0}`);
}

simulateFixed();
