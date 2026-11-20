// Diagnostic browser map workloads. Full acceptance sampling and presentation
// latency remain separate gates; this driver never converts rAF into scanout.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), os = require('node:os');
const crypto = require('node:crypto');
const { chromium } = require('playwright');
const observeWasmMemory = require('./observe_wasm_memory.cjs');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const quantiles = values => {
    const sorted = [...values].sort((a, b) => a - b);
    return Object.fromEntries([['p50', .5], ['p95', .95], ['p99', .99], ['max', 1]].map(([key, q]) => [key, sorted[Math.ceil(q * sorted.length) - 1]]));
};
function visibleCoverage(state) {
    const { x, y, zoom, worldWidth, worldHeight, flat, viewport } = state;
    const top = Math.max(0, Math.ceil(-y / (zoom * 16) - 2));
    const bottom = Math.min(worldHeight - 1, Math.floor((viewport.height - y) / (zoom * 16) + 3));
    let left = Math.ceil(-x / (zoom * 32) - 2), right = Math.floor((viewport.width - x) / (zoom * 32) + 2);
    if (flat) { left = Math.max(left, 0); right = Math.min(right, worldWidth - 1); }
    let distinct = 0, instances = 0;
    for (let row = top; row <= bottom; row++) {
        const columns = new Set();
        for (let col = left + ((left ^ row) & 1); col <= right; col += 2) { columns.add((col % worldWidth + worldWidth) % worldWidth); instances++; }
        distinct += columns.size;
    }
    return { distinct, instances, fraction: distinct / (worldWidth * worldHeight / 2) };
}
async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright) throw Error('Unpinned Playwright');
    const [bundleArg, outputArg, ...flags] = process.argv.slice(2);
    if (!bundleArg || !outputArg) throw Error('Usage: measure_map_browser.cjs BUNDLE OUTPUT [--backend=software|webgl] [--renderer=cpu|gpu] [--size=1920x1080] [--density=1] [--samples=30] [--warmup=10] [--runs=3] [--details=full,half,overview] [--modes=pan,zoom,dirty] [--pan-pattern=oscillate|travel] [--recover-check]');
    const option = (key, fallback) => flags.find(f => f.startsWith(`--${key}=`))?.slice(key.length + 3) ?? fallback;
    const backend = option('backend', 'webgl'), renderer = option('renderer', 'cpu'), gpuFeatures = option('gpu-features', '');
    const [width, height] = option('size', '1920x1080').split('x').map(Number), density = Number(option('density', '1'));
    const count = Number(option('samples', '30')), warmup = Number(option('warmup', '10')), runs = Number(option('runs', '3'));
    const details = option('details', 'full,half,overview').split(','), modes = option('modes', 'pan,zoom,dirty').split(',');
    const panPattern = option('pan-pattern', 'oscillate'), recoveryCheck = flags.includes('--recover-check');
    if (!['oscillate', 'travel'].includes(panPattern) || details.some(d => !['full', 'half', 'overview'].includes(d))
        || modes.some(m => !['pan', 'zoom', 'dirty'].includes(m))) throw Error('Invalid workload');
    if (!['software', 'webgl'].includes(backend) || !['cpu', 'gpu'].includes(renderer) || backend === 'software' && renderer === 'gpu') throw Error('Invalid renderer/backend');
    if (![width, height, density, count, runs].every(n => Number.isInteger(n) && n > 0) || !Number.isInteger(warmup) || warmup < 0) throw Error('Invalid sampling');
    const bundle = path.resolve(bundleArg), output = path.resolve(outputArg);
    fs.mkdirSync(output, { recursive: true }); fs.copyFileSync(__filename, path.join(output, 'driver.cjs'));
    const report = { format: 'openciv2-browser-map-performance/1', completed: false, diagnostic: true, backend, renderer, gpuFeatures,
        physicalViewport: { width, height }, density, count, warmup, runs, details, modes, panPattern, recoveryCheck, flags,
        started: new Date().toISOString(), driverSha256: hash(fs.readFileSync(__filename)),
        scope: 'Production scoped projection and actual Avalonia map/input. Sampled endpoint fresh-frame checks; not every timed frame. Reduced motion; effect workload and physical presentation latency unverified.',
        timing: 'Map renderer entry through Skia lease disposal, excluding lease acquisition and other host work; dirty work adds authored mutation, projection and scene binding. Browser rAF callback duration and GPU timer queries are recorded separately. Input-to-rAF includes driver dispatch and is not scanout latency. Readback and fresh rendering occur after timing.',
        hardware: { platform: os.platform(), release: os.release(), cpus: os.cpus()[0]?.model, threads: os.cpus().length, memory: os.totalmem(), availableMemory: os.freemem(), load: os.loadavg() },
        results: [], captures: [], errors: [] };
    const persist = () => { const file = path.join(output, 'performance.json'); fs.writeFileSync(file + '.tmp', JSON.stringify(report, null, 2)); fs.renameSync(file + '.tmp', file); };
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (name === '/' ? '/index.html' : name));
            if (!file.startsWith(bundle + path.sep)) throw Error('Outside bundle');
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Content-Length': fs.statSync(file).size,
                'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
            fs.createReadStream(file).pipe(response);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    try {
        for (let run = 1; run <= runs; run++) {
            const args = ['--enable-gpu', '--ignore-gpu-blocklist', '--disable-software-rasterizer', '--use-angle=gl', `--force-device-scale-factor=${density}`];
            browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE, args });
            report.browser = browser.version(); report.browserArgs = args;
            const context = await browser.newContext({ viewport: { width: width / density, height: height / density + 140 }, deviceScaleFactor: density });
            await context.addInitScript(observeWasmMemory);
            const page = await context.newPage(); page.setDefaultTimeout(180000);
            page.on('pageerror', error => report.errors.push(String(error)));
            page.on('console', message => fs.appendFileSync(path.join(output, 'console.log'), message.text() + '\n'));
            await page.addInitScript(() => {
                globalThis.mapContexts = []; globalThis.mapRaf = []; globalThis.mapInput = 0;
                for (const name of ['pointermove', 'pointerup', 'wheel', 'keydown']) addEventListener(name, () => globalThis.mapInput = performance.now(), { capture: true });
                const get = HTMLCanvasElement.prototype.getContext;
                HTMLCanvasElement.prototype.getContext = function (type, ...args) {
                    const gl = get.call(this, type, ...args);
                    if (gl && type === 'webgl2' && !mapContexts.some(c => c.gl === gl)) {
                        const info = gl.getExtension('WEBGL_debug_renderer_info');
                        mapContexts.push({ gl, timer: gl.getExtension('EXT_disjoint_timer_query_webgl2'), renderer: info && gl.getParameter(info.UNMASKED_RENDERER_WEBGL) });
                    }
                    return gl;
                };
                const raf = globalThis.requestAnimationFrame.bind(globalThis);
                globalThis.requestAnimationFrame = callback => raf(timestamp => {
                    const source = mapContexts[0], start = performance.now();
                    let query;
                    if (source?.timer && !source.gl.isContextLost() && !source.gl.getQuery(source.timer.TIME_ELAPSED_EXT, source.gl.CURRENT_QUERY)) {
                        query = source.gl.createQuery(); source.gl.beginQuery(source.timer.TIME_ELAPSED_EXT, query);
                    }
                    try { callback(timestamp); }
                    finally {
                        if (query) source.gl.endQuery(source.timer.TIME_ELAPSED_EXT);
                        mapRaf.push({ start, timestamp, cpuMs: performance.now() - start, query, source });
                    }
                });
                globalThis.drainMapRaf = () => mapRaf.splice(0).map(({ query, source, ...sample }) => {
                    if (query) {
                        sample.gpuDisjoint = source.gl.getParameter(source.timer.GPU_DISJOINT_EXT);
                        sample.gpuMs = !sample.gpuDisjoint && source.gl.getQueryParameter(query, source.gl.QUERY_RESULT_AVAILABLE)
                            ? source.gl.getQueryParameter(query, source.gl.QUERY_RESULT) / 1e6 : null;
                        source.gl.deleteQuery(query);
                    }
                    return sample;
                });
            });
            const started = performance.now();
            await page.goto(`http://127.0.0.1:${server.address().port}/?showcase=Mixed&observe=1&backend=${backend}&renderer=${renderer}${gpuFeatures ? '&gpuFeatures=' + encodeURIComponent(gpuFeatures) : ''}`);
            await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().draw?.tiles > 0);
            if (await page.evaluate(() => globalThis.openCiv2Failure)) throw Error(await page.evaluate(() => globalThis.openCiv2Failure));
            report.identity = await page.evaluate(() => openCiv2Identity());
            report.graphics = await page.evaluate(() => mapContexts.map(({ renderer, timer }) => ({ renderer, gpuTimer: !!timer })));
            if (backend === 'webgl' && (!report.graphics.length || report.graphics.some(g => /SwiftShader|llvmpipe/i.test(g.renderer)))) throw Error('Hardware WebGL required');
            await page.evaluate(() => openCiv2PerformanceSetup());
            const read = () => page.evaluate(() => openCiv2PerformanceSnapshot());
            const settle = async () => {
                await page.waitForFunction(() => {
                    const s = openCiv2PerformanceSnapshot();
                    return s.draw && s.draw.x === s.x && s.draw.y === s.y && s.draw.zoom === s.zoom && s.draw.width === s.viewport.width && s.draw.height === s.viewport.height;
                });
                await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
                return read();
            };
            const click = async key => { const box = (await read())[key]; await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2); return settle(); };
            let s = await settle();
            await page.setViewportSize({ width: width / density, height: height / density + Math.round((await page.viewportSize()).height - s.viewport.height) });
            s = await settle();
            if (!s.reducedMotion) await click('motionToggle');
            if (s.suppliedArt) await click('artToggle');
            // Keep pointer hover text from changing footer height and physical map size.
            s = await settle();
            (report.startups ??= []).push({ run, ms: performance.now() - started });
            const place = async (detail, mode) => {
                s = await settle();
                const target = mode === 'zoom' ? .8 : detail === 'full' ? 1 : detail === 'half'
                    ? Math.max(.25, Math.sqrt(s.viewport.width * s.viewport.height / (252 * 252 * 32 * 16 * .5))) : .25;
                const x = s.viewport.width * .5, y = s.viewport.y + s.viewport.height * .55;
                const delta = Math.log(target / s.zoom) / Math.log(1.25) * 80;
                await page.keyboard.down('Shift'); await page.mouse.move(x, y); await page.mouse.down();
                await page.mouse.move(x, y - delta); await page.mouse.up(); await page.keyboard.up('Shift');
                await settle(); s = await click('clear');
                const box = s.overview;
                await page.mouse.click(box.x + box.width * .25, box.y + box.height * .43); await settle();
                await page.mouse.move(5, 5); s = await settle();
                // Footer may wrap at 2x density. Correct physical extent after placement.
                const viewport = page.viewportSize();
                if (Math.abs(s.viewport.height * density - height) > .1) {
                    await page.setViewportSize({ width: width / density, height: viewport.height + height / density - s.viewport.height });
                    s = await settle();
                }
                if (Math.abs(s.viewport.width * density - width) > .1 || Math.abs(s.viewport.height * density - height) > .1) throw Error('Wrong physical map extent');
            };
            const capture = async name => {
                await page.mouse.move(5, 5); const state = await settle();
                await page.screenshot({ path: path.join(output, name + '.png') });
                const fresh = await page.evaluate(() => openCiv2FreshRendererFrame());
                fs.writeFileSync(path.join(output, name + '-fresh.png'), Buffer.from(fresh, 'base64'));
                if (renderer === 'gpu') {
                    const cpu = await page.evaluate(async () => {
                        const bytes = await openCiv2FreshFrame();
                        let binary = '';
                        for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
                        return btoa(binary);
                    });
                    fs.writeFileSync(path.join(output, name + '-cpu.png'), Buffer.from(cpu, 'base64'));
                }
                let recoveredState;
                if (recoveryCheck) {
                    await click('recover'); await page.mouse.move(5, 5); recoveredState = await settle();
                    if (recoveredState.changedDraw.sequence <= state.changedDraw.sequence || recoveredState.changedDraw.preparedChunks <= 0)
                        throw Error('Resource recovery did not produce a fresh map');
                    for (const key of ['x', 'y', 'zoom', 'revision', 'scaling']) if (recoveredState[key] !== state[key]) throw Error(`Recovery changed ${key}`);
                    await page.screenshot({ path: path.join(output, name + '-recovered.png') });
                }
                report.captures.push({ name, state, recoveredState }); persist();
            };
            for (const detail of details) for (const mode of modes) {
                if (mode === 'zoom' && detail !== details[0]) continue;
                await place(detail, mode);
                const samples = [], initial = await read();
                const row = { run, detail: mode === 'zoom' ? 'continuous' : detail, mode, initial, coverage: visibleCoverage(initial), samples, warmupSamples: [] };
                report.results.push(row); persist();
                const center = { x: initial.viewport.width * .48, y: initial.viewport.y + initial.viewport.height * .5 };
                if (mode === 'pan' || mode === 'zoom') {
                    if (mode === 'zoom') await page.keyboard.down('Shift');
                    await page.mouse.move(center.x, center.y); await page.mouse.down();
                    await page.mouse.move(center.x + 7, center.y); await settle();
                }
                let lastPan = { x: center.x + 7, y: center.y };
                for (let index = -warmup; index < count; index++) {
                    const before = await read();
                    await page.evaluate(() => drainMapRaf());
                    let pointerDeltaPixels;
                    if (mode === 'pan') {
                        const step = index + warmup;
                        const wave = 80 - Math.abs((step + 1) % 160 - 80);
                        const point = panPattern === 'travel'
                            ? index === 0 ? { x: lastPan.x + 1 / density, y: lastPan.y }
                                : { x: center.x + 7 + 5 * wave, y: center.y + 2 * wave }
                            : { x: center.x + 8 + (step % 2 ? 24 : 0), y: center.y + (step % 2 ? 12 : 0) };
                        pointerDeltaPixels = [(point.x - lastPan.x) * density, (point.y - lastPan.y) * density];
                        await page.mouse.move(point.x, point.y); lastPan = point;
                    } else if (mode === 'zoom') {
                        const step = index + warmup, phase = (step + 1) / 40.3;
                        await page.mouse.move(center.x + 7, center.y + 160 * Math.sin(phase * Math.PI * 2));
                    } else if (mode === 'dirty') await click('change');
                    else throw Error(`Unknown mode ${mode}`);
                    await page.waitForFunction(sequence => openCiv2PerformanceSnapshot().changedDraw.sequence > sequence, before.changedDraw.sequence);
                    const after = await settle();
                    const timing = await page.evaluate(() => ({ raf: drainMapRaf(), inputToObservedRafMs: performance.now() - mapInput }));
                    const draw = after.changedDraw;
                    if (Math.abs(draw.width * density - width) > .1 || Math.abs(draw.height * density - height) > .1) throw Error('Measured viewport changed size');
                    if (draw.error || draw.rendererFallback || draw.composition !== renderer) throw Error('Renderer failed or fell back: ' + JSON.stringify(draw));
                    if (mode === 'pan' && draw.x === before.changedDraw.x && draw.y === before.changedDraw.y || mode === 'zoom' && draw.zoom === before.changedDraw.zoom) throw Error('Input did not change camera');
                    const update = mode === 'dirty' ? after.updateWork : null;
                    const sample = { index, draw, update, pointerDeltaPixels, frameWorkMs: (draw.callbackMs ?? draw.ms) + (update ? update.authoredMs + update.bindMs : 0),
                        managedAllocatedBytes: after.managedAllocatedBytes - before.managedAllocatedBytes, gcPauseMs: after.gcPauseMs - before.gcPauseMs, gcCollections: after.gcCollections.map((n, i) => n - before.gcCollections[i]), ...timing };
                    if (index >= 0) samples.push(sample); else row.warmupSamples.push(sample);
                    if (index % 10 === 0) persist();
                }
                if (mode === 'pan' || mode === 'zoom') { await page.mouse.up(); if (mode === 'zoom') await page.keyboard.up('Shift'); }
                row.summary = Object.fromEntries(['frameWorkMs', 'managedAllocatedBytes', 'gcPauseMs', 'inputToObservedRafMs'].map(key => [key, quantiles(samples.map(s => s[key]))]));
                row.memory = await page.evaluate(() => oc2WasmCapacity());
                row.warmDrawTargetMet = row.summary.frameWorkMs.p95 <= 16.7 && row.summary.frameWorkMs.p99 <= 33.3;
                await capture(`run-${run}-${detail}-${mode}`);
                console.log(`${backend}/${renderer} ${width}x${height}@${density} run ${run} ${detail}/${mode}: ${JSON.stringify(row.summary.frameWorkMs)}`);
                persist();
            }
            await browser.close(); browser = null;
        }
        if (report.errors.length) throw Error(report.errors.join('\n'));
        report.completed = true; report.finished = new Date().toISOString(); persist();
    } catch (error) { report.failure = String(error); persist(); throw error; }
    finally { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
