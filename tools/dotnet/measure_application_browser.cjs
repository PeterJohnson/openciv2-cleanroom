// Production Avalonia navigation measurements. No substitute UI or engine calls.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const { chromium } = require('playwright');
const observeWasmMemory = require('./observe_wasm_memory.cjs');

async function main() {
    const version = require('playwright/package.json').version;
    if (version !== require('../../dependencies.json').tools.playwright) throw new Error('Unpinned Playwright');
    const [bundleArg, reportArg, countArg = '15'] = process.argv.slice(2);
    const bundle = path.resolve(bundleArg), count = Number(countArg);
    if (!Number.isInteger(count) || count < 1 || count > 30) throw new Error('Invalid population size');
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const requests = [];
    const server = http.createServer((request, response) => {
        try {
            const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (pathname === '/' ? '/index.html' : pathname));
            if (!file.startsWith(bundle + path.sep)) throw new Error('Outside bundle');
            const original = fs.statSync(file).size;
            const encoding = /\bbr\b/.test(request.headers['accept-encoding'] || '') && fs.existsSync(file + '.br') ? 'br' : null;
            const bytes = fs.readFileSync(file + (encoding ? '.br' : ''));
            const headers = { 'Content-Type': types[path.extname(file)] || 'application/octet-stream',
                'Cache-Control': 'public, max-age=3600', 'Vary': 'Accept-Encoding', 'Content-Length': bytes.length,
                'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' };
            if (encoding) headers['Content-Encoding'] = encoding;
            requests.push({ path: pathname, encoding, bodyBytes: bytes.length, decodedBytes: original });
            response.writeHead(200, headers); response.end(bytes);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    let browser;
    const samples = [], errors = [];
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE,
            args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--force-device-scale-factor=1'] });
        let identity;
        for (let iteration = 0; iteration < count + 3; ++iteration) {
            // A new context has no HTTP/IndexedDB cache; the following navigation
            // retains that context's caches. OS and browser-engine caches are uncontrolled.
            const context = await browser.newContext({ viewport: { width: 1000, height: 800 }, deviceScaleFactor: 1 });
            await context.addInitScript(observeWasmMemory);
            await context.addInitScript(() => {
                performance.setResourceTimingBufferSize(5000);
                const ready = () => {
                    try {
                        const s = globalThis.openCiv2Snapshot?.();
                        if (s?.controls?.send?.enabled && s.controls.send.width > 0 && s.observer === '1' && s.units?.map(u => u.slot).join(',') === '0,1,2') {
                            // Observe another paint opportunity after the model and layout exist.
                            requestAnimationFrame(() => { globalThis.oc2ReadyMs = performance.now(); }); return;
                        }
                    } catch { /* Exports may be installed before the view exists. */ }
                    requestAnimationFrame(ready);
                };
                requestAnimationFrame(ready);
            });
            const page = await context.newPage(); page.on('pageerror', error => errors.push(String(error)));
            const cdp = await context.newCDPSession(page); await cdp.send('Network.enable');
            let network = [];
            cdp.on('Network.loadingFinished', event => network.push(event.encodedDataLength));
            for (const cache of ['cold', 'warm']) {
                if (cache === 'warm') await page.goto('about:blank');
                const firstRequest = requests.length; network = [];
                await page.goto(`http://127.0.0.1:${server.address().port}/`, { timeout: 90000 });
                await page.waitForFunction(() => globalThis.oc2ReadyMs !== undefined || globalThis.openCiv2Failure, undefined, { timeout: 90000 });
                const failure = await page.evaluate(() => globalThis.openCiv2Failure);
                if (failure) throw new Error(failure);
                await page.waitForLoadState('networkidle', { timeout: 30000 });
                const state = await page.evaluate(() => ({ readyMs: globalThis.oc2ReadyMs, snapshot: globalThis.openCiv2Snapshot(),
                    memory: globalThis.oc2WasmCapacity(), resources: performance.getEntriesByType('resource').map(r => ({ name: new URL(r.name).pathname,
                        transferSize: r.transferSize, encodedBodySize: r.encodedBodySize, decodedBodySize: r.decodedBodySize })) }));
                const currentIdentity = await page.evaluate(() => globalThis.openCiv2Identity());
                if (identity && JSON.stringify(identity) !== JSON.stringify(currentIdentity)) throw new Error('Application identity changed');
                identity = currentIdentity;
                if (!state.memory.memories || state.memory.bytes <= 0) throw new Error('Runtime WASM memory was not observed');
                if (Math.abs(state.snapshot.width - 1000) > 2 || state.snapshot.units.length !== 3 || state.snapshot.scaling !== 1) throw new Error('Invalid ready UI');
                const sample = { iteration, warmup: iteration < 3, cache, ...state, networkEncodedBytes: network.reduce((a, b) => a + b, 0),
                    serverRequests: requests.slice(firstRequest) };
                if (iteration === 3 && cache === 'cold') await page.screenshot({ path: reportArg + '.ready.png' });
                // Verify readiness by real input after freezing startup/transfer samples.
                const click = async name => { const b = (await page.evaluate(() => globalThis.openCiv2Snapshot())).controls[name];
                    if (!b.enabled) throw new Error('Disabled input'); await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2); };
                await click('send'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().status === 'AwaitingInteraction');
                await click('confirm'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().status === 'Completed');
                sample.completed = await page.evaluate(() => globalThis.openCiv2Snapshot());
                sample.memoryAfterCommand = await page.evaluate(() => globalThis.oc2WasmCapacity());
                if (!sample.completed.changed || sample.completed.effects !== 1) throw new Error('Ready UI failed real command');
                samples.push(sample);
            }
            await context.close();
            console.log(`Completed navigation pair ${iteration + 1}/${count + 3}`);
        }
        if (errors.length) throw new Error(errors.join('\n'));
        fs.writeFileSync(reportArg, JSON.stringify({ format: 'openciv2-application-startup/1', host: 'browser',
            browser: browser.version(), playwright: version, identity, samples, errors }, null, 2) + '\n');
    } finally { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
