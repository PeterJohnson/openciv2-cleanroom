# Warmed production workload measurements

Two native Release runs and two real Chromium WASM runs pass the fixed operation
latency and allocation budgets. Each native population has three warmups and forty
measured calls; WASM has three warmups and fifteen measured calls. The
[receipt](warm.json) preserves all 976 samples, inputs, output hashes,
compiled/report/artifact identities and host details. Every workload produces the
same checked result identity across native and WASM. This closes warmed-operation
collection; [production application startup/transfer](application.md)
is also measured separately. The [performance acceptance record](README.md)
records the investigated tradeoffs.

Measurements use the same Linux x64 Ryzen AI 9 HX 370 host, .NET SDK 10.0.404 and
runtime 10.0.15 as the recorded CLI workload study. Browser execution uses Chromium
151.0.7922.34 and Playwright 1.58.2, with the published trimmed Release WASM host.
The table below retains the first browser population; the second full run adds
the peak memory bound described below. Both execute the production libraries
without Avalonia. No Mac/Windows/ARM64 execution  is implied.

| Workload | Native median range (ms) | Native worst p95 (ms) | Browser median / p95 (ms) | Native / browser max allocated MiB per call |
| --- | ---: | ---: | ---: | ---: |
| Decode large save | 2.67–2.67 | 4.13 | 11.6 / 16.4 | 1.75 / 1.56 |
| Full inspection and JSON | 106.4–109.4 | 132.6 | 815.3 / 897.6 | 159.52 / 145.04 |
| Two inspections, diff and JSON | 152.7–153.7 | 176.3 | 497.7 / 529.4 | 130.03 / 90.46 |
| Three-manifest pack resolution | 0.52–0.52 | 0.80 | 10.8 / 25.2 | 0.47 / 0.47 |
| Complete rule conversion | 8.65–9.23 | 15.44 | 58.0 / 73.2 | 7.34 / 5.61 |
| Create populated world | 0.19–0.20 | 0.59 | 1.3 / 2.8 | 0.74 / 0.71 |
| Owned unit and city projections | 0.15–0.16 | 0.20 | 0.5 / 1.2 | 0.18 / 0.12 |
| Confirmed stack command and retirement | 0.010–0.011 | 0.013 | 0.2 / 0.3 | 0.0073 / 0.0067 |

Nearest-rank p95 with fifteen browser samples is the maximum. Raw maxima, GC
counts and per-sample allocations remain in the receipt. The allocation ceiling
is checked against the maximum sample, not an average. Warmups are preserved but
excluded from these summaries.

## Costs and interpretation

Inspection and diff dominate allocation and native GC time. The first native
run records 1,599 ms and 3,084 ms of GC pauses across their forty measured calls,
respectively. Production inspection builds owned records and fields for every
stored record; formatting also constructs the complete 21,316,847-byte JSON
result. Diff builds two complete projections. These are explicit whole-file
analysis operations, and their allocation profiles must not be used for per-frame
UI work. A possible later streaming analysis path would need to preserve complete
owned results and cancellation/error behavior; no pooling or representation
change was made solely to improve this benchmark.

The full owner projection is much smaller: both 2,048 units and 256 named cities
cost about 185 KiB and 0.15–0.16 ms per native call, or 126 KiB and 0.5 ms in WASM.
This is actual production model/query cost to accompany the retained map-rendering
comparison. It includes Session lease checks and owned result creation. The
application should continue querying on semantic updates, not create a world or
copy all analysis records every frame. These timings make no display-FPS claim.

The content measurements also separate operation cost from the earlier CLI
results: pack resolution is about 0.52 ms warmed versus 85–92 ms for a fresh
managed process, and conversion about 9 ms versus 149 ms. Initialization is a
material candidate in the small-command CLI regressions. These are different
execution boundaries, so subtracting their medians would not isolate JIT or disk
cost. They are not interchangeable performance populations. The retained
17.6–20.6 ms managed help startup separately bounds minimal process entry.

For this finite workload, retaining the clear owned analysis model and JIT-based
native build is a reasonable engineering tradeoff: repeated operation and CLI
absolute budgets pass with substantial headroom, while the view/command path
stays small. SQLite is measured separately; these portable operations do not isolate SQL/interop cost.

## Measurement limits

Input loading, stack reset and result verification are outside timing. Verification
allocates; its garbage can affect subsequent measured collections. No forced GC
or slow-sample removal is used. The runner validates exact round-trip bytes,
complete inspection record closure/JSON extent, the authored single-field diff,
resolved manifest/component identities, both converted rule components, all
initialized units/cities, exact owner-query records and the changed stack/effect.
Existing behavior suites remain the broader correctness evidence.

WASM GC pause duration is unavailable: its API returned zero while collections
were occurring, so samples use `null`. Collection counters are runtime reports;
Mono's generation numbering should not be equated with CoreCLR generations.
Observed post-call managed heap reaches 378.5 MiB in WASM; the largest pre-call
heap estimate plus all operation allocations is 515.2 MiB, below the 768 MiB
budget. These are managed estimates, not a continuous heap or resident-memory
peak. A second full browser run observes the actual monotonic WASM memory
capacity at 531.7 MiB, conservatively bounding its managed peak below 768 MiB;
see the [application/memory report](application.md#peak-wasm-memory-bound).

See the [protocol](README.md) for exact operation
boundaries and reproduction commands. `test_warm_report.py` rejects omitted
workloads/samples, altered input/source identities, invalid counters and inconsistent
outputs, and retains an over-budget sample as a failure. The five original groups
pass against both native reports and both browser reports; a sixth group checks
the observed WASM capacity and rejects an excessive bound when present.
