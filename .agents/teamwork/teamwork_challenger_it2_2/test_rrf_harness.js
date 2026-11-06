/**
 * Empirical Test Harness 2: RRF Score Gating & Payload Normalization
 * Tests the exact logic from PRODUCTION_ROADMAP.md §5.3
 */

function normalizeChunkPayload(rawHit, sourceType) {
    let chunkId, docId, text, courseId, moduleId, lessonId, pageNum, title, visibility, score;

    if (rawHit && typeof rawHit === 'object' && !(rawHit.payload !== undefined || rawHit.score !== undefined && rawHit.id !== undefined && typeof rawHit.score === 'number' && rawHit.payload === undefined)) {
        // Dict-like
        chunkId = String(rawHit.chunk_id || rawHit.id || "");
        docId = rawHit.document_id;
        text = rawHit.text || "";
        courseId = rawHit.course_id;
        moduleId = rawHit.module_id;
        lessonId = rawHit.lesson_id;
        pageNum = rawHit.page_number || 1;
        title = rawHit.section_title || rawHit.title || "";
        visibility = rawHit.visibility || "student";
        score = Number(rawHit.score || rawHit.bm25_score || 0.0);
    } else {
        // Object-like (e.g. Qdrant ScoredPoint)
        chunkId = String(rawHit.id || "");
        const payload = rawHit.payload || {};
        docId = payload.document_id;
        text = payload.text || "";
        courseId = payload.course_id;
        moduleId = payload.module_id;
        lessonId = payload.lesson_id;
        pageNum = payload.page_number || 1;
        title = payload.section_title || "";
        visibility = payload.visibility || "student";
        score = Number(rawHit.score || 0.0);
    }

    return {
        id: chunkId,
        chunk_id: chunkId,
        document_id: docId,
        course_id: courseId,
        module_id: moduleId,
        lesson_id: lessonId,
        page_number: pageNum,
        section_title: title,
        visibility: visibility,
        text: text,
        raw_score: score,
        source_type: sourceType
    };
}

function fuseAndRerank(denseHits, sparseHits, query, topK = 4, minDenseCosine = 0.40, minSparseBm25 = 1.0, rrfK = 60) {
    const scores = {};
    const docs = {};

    // 1. Process & Gate Dense Hits (Cosine >= 0.40)
    const gatedDense = [];
    for (const hit of denseHits) {
        const norm = normalizeChunkPayload(hit, "dense");
        if (norm.raw_score >= minDenseCosine) {
            gatedDense.push(norm);
        }
    }

    for (let rank = 1; rank <= gatedDense.length; rank++) {
        const hit = gatedDense[rank - 1];
        const cid = hit.chunk_id;
        docs[cid] = hit;
        scores[cid] = (scores[cid] || 0.0) + (0.65 / (rrfK + rank));
    }

    // 2. Process & Gate Sparse Hits (BM25 >= 1.0)
    const gatedSparse = [];
    for (const hit of sparseHits) {
        const norm = normalizeChunkPayload(hit, "sparse");
        if (norm.raw_score >= minSparseBm25) {
            gatedSparse.push(norm);
        }
    }

    for (let rank = 1; rank <= gatedSparse.length; rank++) {
        const hit = gatedSparse[rank - 1];
        const cid = hit.chunk_id;
        if (!docs[cid]) {
            docs[cid] = hit;
        }
        scores[cid] = (scores[cid] || 0.0) + (0.35 / (rrfK + rank));
    }

    if (Object.keys(scores).length === 0) {
        return [];
    }

    // 3. Rank Top Candidates by Fused RRF Score
    const sortedIds = Object.keys(scores).sort((a, b) => scores[b] - scores[a]).slice(0, 10);

    // 4. Return canonical chunks
    const finalChunks = [];
    for (let idx = 1; idx <= Math.min(topK, sortedIds.length); idx++) {
        const did = sortedIds[idx - 1];
        const item = Object.assign({}, docs[did]);
        item.rrf_rank = idx;
        item.fused_score = Number(scores[did].toFixed(5));
        finalChunks.push(item);
    }

    return finalChunks;
}

function runRrfTests() {
    console.log("=== EMPIRICAL TEST: RRF RELEVANCE SCORE GATING & PAYLOAD NORMALIZATION ===");

    // Test Scenario 1: Noise Inversion Test
    // Candidate A (Doc 101): High semantic relevance (0.96), sparse 0
    // Candidate B (Doc 102): Cafeteria noise (cosine 0.18, BM25 0.05)
    // Candidate C (Doc 103): Sub-threshold noise (cosine 0.39, BM25 0.95)
    // Candidate D (Doc 104): High lexical relevance (cosine 0.35, BM25 14.5)

    const denseHits = [
        { id: "doc_101", score: 0.96, payload: { document_id: 10, text: "Dependency Injection in ASP.NET MVC 5" } },
        { id: "doc_102", score: 0.18, payload: { document_id: 20, text: "Cafeteria open 8 AM to 4 PM" } },
        { id: "doc_103", score: 0.39, payload: { document_id: 30, text: "Library lost and found notice" } },
        { id: "doc_104", score: 0.35, payload: { document_id: 40, text: "UnityContainer configuration steps" } }
    ];

    const sparseHits = [
        { id: "doc_104", score: 14.5, text: "UnityContainer configuration steps", course_id: 1 },
        { id: "doc_102", score: 0.05, text: "Cafeteria open 8 AM to 4 PM", course_id: 1 },
        { id: "doc_103", score: 0.95, text: "Library lost and found notice", course_id: 1 }
    ];

    console.log("\n1. Testing Ungated RRF Baseline (Iteration 1 Failure Mode):");
    // Ungated:
    const ungated = fuseAndRerank(denseHits, sparseHits, "query", 4, -1.0, -1.0);
    console.log("Ungated ranking order:", ungated.map(c => ({ id: c.id, fused: c.fused_score, text: c.text })));
    const ungatedNoise = ungated.find(c => c.id === "doc_102");
    const ungatedDoc101 = ungated.find(c => c.id === "doc_101");
    console.log(`Ungated: Noise doc_102 score = ${ungatedNoise.fused_score}, High-relevance doc_101 score = ${ungatedDoc101.fused_score}`);
    console.log(`Noise inversion present in ungated? ${ungatedNoise.fused_score > ungatedDoc101.fused_score} (doc_102 is ${((ungatedNoise.fused_score / ungatedDoc101.fused_score - 1) * 100).toFixed(1)}% higher)`);

    console.log("\n2. Testing Gated RRF (Roadmap §5.3: Cosine >= 0.40, BM25 >= 1.0):");
    const gated = fuseAndRerank(denseHits, sparseHits, "query", 4, 0.40, 1.0);
    console.log("Gated ranking order:", gated.map(c => ({ id: c.id, fused: c.fused_score, text: c.text, source: c.source_type })));

    // Verify Candidate B (doc_102) and Candidate C (doc_103) are completely eliminated
    const noisePresent = gated.some(c => c.id === "doc_102" || c.id === "doc_103");
    console.log(`Are noise documents (doc_102, doc_103) filtered out? ${!noisePresent}`);
    console.log(`Is doc_101 ranked #1 or #2? ${gated.some(c => c.id === "doc_101")}`);
    console.log(`Is lexical match doc_104 included? ${gated.some(c => c.id === "doc_104")}`);

    console.log("\n3. Testing Schema Normalization & Downstream Citation Safety:");
    for (const chunk of gated) {
        console.log(`Checking chunk ${chunk.id}: has 'id'? ${'id' in chunk}, has 'chunk_id'? ${'chunk_id' in chunk}, has 'document_id'? ${'document_id' in chunk}`);
        if (!('id' in chunk) || !('chunk_id' in chunk) || !('document_id' in chunk)) {
            console.error(`FAIL: Missing key in chunk ${chunk.id}`);
            process.exit(1);
        }
    }

    if (!noisePresent && gated.length === 2 && gated[0].id === "doc_101" && gated[1].id === "doc_104") {
        console.log("\n>>> RRF RELEVANCE SCORE GATING & PAYLOAD NORMALIZATION VERIFIED! <<<");
    } else {
        console.error("FAIL: Gated RRF output did not match expected safe set!");
        process.exit(1);
    }
}

runRrfTests();
