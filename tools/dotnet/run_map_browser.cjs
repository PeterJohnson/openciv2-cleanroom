// P02 acceptance: shared exhaustive cases in WASM, plus actual production WebGL/input.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const crypto = require('node:crypto');
const { chromium } = require('playwright');
const cases = Array.from({ length: 30 }, (_, i) => `MV${String(i + 1).padStart(2, '0')}`).concat('Mixed');
const gallerySize = { width: 2250, height: 1650 }; // Same complete selector-board extent as native acceptance.
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const driverSha256 = hash(fs.readFileSync(__filename));
async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright) throw Error('Unpinned Playwright');
    const [bundleArg, outputArg, ...flags] = process.argv.slice(2);
    if (!bundleArg || !outputArg) throw Error('Usage: run_map_browser.cjs BUNDLE OUTPUT [--matrix-only|--gpu-history-only|--gallery-only|--lifecycle-only|--transitions-only|--smoke] [--renderer=cpu|gpu|default] [--hardware]');
    const hardware = flags.includes('--hardware');
    const rendererChoice = flags.find(flag => flag.startsWith('--renderer='))?.split('=')[1] ?? 'cpu';
    if (!['cpu', 'gpu', 'default'].includes(rendererChoice)) throw Error('Unknown renderer');
    const renderer = rendererChoice === 'default' ? 'cpu' : rendererChoice;
    const gpuFeatures = flags.find(flag => flag.startsWith('--gpu-features='))?.split('=')[1] ?? '';
    const rendererQuery = (rendererChoice === 'default' ? '' : '&renderer=' + renderer) +
        (gpuFeatures ? '&gpuFeatures=' + encodeURIComponent(gpuFeatures) : '');
    const historyOnly = flags.includes('--gpu-history-only');
    const expectedFeatures = gpuFeatures.split(',').map(s => s.trim()).sort().join(',');
    const matchesFeatures = s => !gpuFeatures || s.draw.gpuFeatures.split(',').map(s => s.trim()).sort().join(',') === expectedFeatures;
    const lifecycleOnly = flags.includes('--lifecycle-only');
    const transitionsOnly = flags.includes('--transitions-only');
    const selectedDensity = flags.find(flag => flag.startsWith('--density='))?.split('=')[1];
    const selectedTheme = flags.find(flag => flag.startsWith('--theme='))?.split('=')[1];
    if (selectedDensity && !['1', '2'].includes(selectedDensity)) throw Error('Unknown density');
    if (selectedTheme && !['stock', 'native', 'classic'].includes(selectedTheme)) throw Error('Unknown theme');
    const bundle = path.resolve(bundleArg), output = path.resolve(outputArg);
    fs.mkdirSync(output, { recursive: true });
    fs.copyFileSync(__filename, path.join(output, 'driver.cjs'));
    const requests = [], checks = [], captures = [], errors = [];
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            if (name === '/favicon.ico') { response.writeHead(204); response.end(); return; }
            const file = path.resolve(bundle, '.' + (name === '/' ? '/index.html' : name));
            if (!file.startsWith(bundle + path.sep)) throw Error('Outside bundle');
            const size = fs.statSync(file).size;
            requests.push({ path: name, bytes: size });
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Content-Length': size,
                'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
            fs.createReadStream(file).pipe(response);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser, page, identity;
    const persist = completed => {
        const report = { format: 'openciv2-browser-map-acceptance/1', completed, flags, browser: browser.version(),
            backend: hardware ? 'WebGL2 / hardware; functional evidence, no physical presentation claim' : 'WebGL2 / SwiftShader; functional fallback evidence, no physical performance claim', renderer, rendererChoice, gpuFeatures,
            identity, checks, captures, requests, errors, driverSha256 };
        const temporary = path.join(output, 'browser.json.tmp');
        fs.writeFileSync(temporary, JSON.stringify(report, null, 2));
        fs.renameSync(temporary, path.join(output, 'browser.json'));
    };
    try {
        for (const density of selectedDensity ? [Number(selectedDensity)] : flags.includes('--matrix-only') || historyOnly || lifecycleOnly ? [1] : [1, 2]) {
            if (browser) await browser.close();
            browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE,
                args: [...(hardware ? ['--enable-gpu', '--ignore-gpu-blocklist', '--disable-software-rasterizer', '--use-angle=gl']
                    : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader']), `--force-device-scale-factor=${density}`] });
            for (const theme of selectedTheme ? [selectedTheme] : flags.includes('--smoke') || flags.includes('--matrix-only') || historyOnly || lifecycleOnly || transitionsOnly ? ['stock'] : ['stock', 'native', 'classic']) {
                const context = await browser.newContext({ viewport: gallerySize, deviceScaleFactor: density });
                page = await context.newPage();
                page.setDefaultTimeout(60000);
                page.on('pageerror', error => errors.push(String(error)));
                page.on('console', message => {
                    fs.appendFileSync(path.join(output, 'console.log'), message.text() + '\n');
                    if (message.type() === 'error') console.error('Browser:', message.text());
                    else if (/^(MV\d|Mixed|Presentation|Native matrix|GPU history|Optimization checks)/.test(message.text())) console.log(message.text());
                });
                await page.addInitScript(() => {
                    globalThis.mapWebGl = [];
                    globalThis.mapWebGlContexts = [];
                    globalThis.mapContextEvents = [];
                    const original = HTMLCanvasElement.prototype.getContext;
                    HTMLCanvasElement.prototype.getContext = function (type, ...args) {
                        const context = original.call(this, type, ...args);
                        if (context && type === 'webgl2') {
                            const extension = context.getExtension('WEBGL_debug_renderer_info');
                            globalThis.mapWebGl.push({ type, version: context.getParameter(context.VERSION),
                                renderer: extension ? context.getParameter(extension.UNMASKED_RENDERER_WEBGL) : context.getParameter(context.RENDERER) });
                            if (!globalThis.mapWebGlContexts.includes(context)) {
                                globalThis.mapWebGlContexts.push(context);
                                this.addEventListener('webglcontextlost', () => globalThis.mapContextEvents.push('lost'));
                                this.addEventListener('webglcontextrestored', () => globalThis.mapContextEvents.push('restored'));
                            }
                        }
                        return context;
                    };
                });
                const started = performance.now();
                await page.goto(`http://127.0.0.1:${server.address().port}/?showcase=MV01&observe=1${rendererQuery}${theme === 'stock' ? '' : '&art=' + theme}`);
                await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().draw?.tiles > 0, undefined, { timeout: 180000 });
                if (await page.evaluate(() => globalThis.openCiv2Failure)) throw Error(await page.evaluate(() => globalThis.openCiv2Failure));
                const currentIdentity = await page.evaluate(() => globalThis.openCiv2Identity());
                if (identity && JSON.stringify(identity) !== JSON.stringify(currentIdentity)) throw Error('Compiled browser inputs changed during acceptance');
                identity = currentIdentity;
                const read = () => page.evaluate(() => globalThis.openCiv2Snapshot());
                const settle = async () => {
                    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
                    await page.waitForFunction(() => {
                        const s = globalThis.openCiv2Snapshot();
                        return s.draw && s.draw.x === s.x && s.draw.y === s.y && s.draw.zoom === s.zoom &&
                            s.draw.width === s.viewport.width && s.draw.height === s.viewport.height;
                    });
                };
                const wait = async predicate => {
                    await page.waitForFunction(predicate);
                    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
                    return read();
                };
                const click = async name => {
                    const box = (await read())[name];
                    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
                };
                const assert = async (name, predicate) => {
                    await settle();
                    const state = await read();
                    if (!predicate(state)) throw Error(`${theme}/${density}: ${name}: ${JSON.stringify(state)}`);
                    checks.push({ name, theme, density, state });
                    fs.writeFileSync(path.join(output, 'progress.json'), JSON.stringify({ name, theme, density, checks: checks.length, captures: captures.length }));
                    persist(false);
                };
                const select = async (id, quiet = true) => {
                    const before = cases.indexOf((await read()).case), target = cases.indexOf(id);
                    await click('selector');
                    const delta = target - before;
                    for (let i = 0; i < Math.abs(delta); i++) await page.keyboard.press(delta > 0 ? 'ArrowDown' : 'ArrowUp');
                    await page.keyboard.press('Enter');
                    await page.waitForFunction(id => globalThis.openCiv2Snapshot().case === id, id);
                    await settle();
                    if ((await read()).reducedMotion !== quiet) await click('motionToggle');
                    await settle();
                };
                const worldPoint = (s, x, y) => [s.viewport.x + s.x + x * 32 * s.zoom, s.viewport.y + s.y + y * 16 * s.zoom];
                const capture = async label => {
                    await page.mouse.move(5, 5);
                    await settle();
                    const name = `${theme}-${density}x-${label}`;
                    await page.screenshot({ path: path.join(output, name + '.png') });
                    const state = await read(), id = state.case;
                    fs.writeFileSync(path.join(output, name + '.json'), JSON.stringify(state, null, 2));
                    const fresh = await page.evaluate(async renderer => {
                        if (renderer === 'gpu') return globalThis.openCiv2FreshRendererFrame();
                        const bytes = await globalThis.openCiv2FreshFrame();
                        let binary = ''; for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
                        return btoa(binary);
                    }, renderer);
                    fs.writeFileSync(path.join(output, name + '-fresh.png'), Buffer.from(fresh, 'base64'));
                    captures.push({ id, theme, density, name, state });
                    await assert(label + ' production frame', s => s.case === id && s.draw.tiles > 0 && s.draw.error === null && s.scaling === density && s.draw.composition === renderer && !s.draw.rendererFallback &&
                        (renderer !== 'gpu' || matchesFeatures(s)));
                };
                const gl = await page.evaluate(() => globalThis.mapWebGl);
                if (!gl.length) throw Error('Production canvas did not create WebGL2');
                if (hardware && gl.some(g => /SwiftShader|llvmpipe/i.test(g.renderer))) throw Error('Hardware WebGL required');
                checks.push({ name: 'pinned-content-and-WebGL2', theme, density, startupMs: performance.now() - started, gl });
                await assert('startup renderer selection', s => s.draw.composition === renderer && !s.draw.rendererFallback &&
                    (renderer !== 'gpu' || matchesFeatures(s)));
                console.log(`Loaded ${theme} at ${density}×: ${gl[0].renderer}`);
                if (historyOnly || density === 1 && theme === 'stock' && !flags.includes('--gallery-only') && !flags.includes('--smoke') && !lifecycleOnly && !transitionsOnly) {
                    console.log(historyOnly ? 'Running shared GPU history/lifecycle matrix' : 'Running exhaustive shared WASM matrix');
                    const matrix = await page.evaluate(history => history ? globalThis.openCiv2GpuHistory() : globalThis.openCiv2Matrix(), historyOnly);
                    fs.writeFileSync(path.join(output, historyOnly ? 'gpu-history.json' : 'matrix.json'), JSON.stringify(matrix, null, 2));
                    const files = await page.evaluate(() => globalThis.openCiv2ResultFiles());
                    for (const file of files) {
                        const encoded = await page.evaluate(file => {
                            const bytes = globalThis.openCiv2ResultFile(file);
                            let binary = ''; for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
                            return btoa(binary);
                        }, file);
                        const destination = path.join(output, 'wasm', file.slice('/results/'.length));
                        fs.mkdirSync(path.dirname(destination), { recursive: true });
                        fs.writeFileSync(destination, Buffer.from(encoded, 'base64'));
                    }
                    if (historyOnly && (matrix.checks !== 204 || matrix.historyFailures !== 0 ||
                        matrix.rows.some(row => !row.cpuWithinBounds) || matrix.lifecycle.warmedReplacements !== 100 || !matrix.lifecycle.exact || !matrix.lifecycle.bounded))
                        throw Error('GPU history, CPU bounds or replacement checks failed');
                    if (historyOnly && matrix.features.split(', ').some(feature => ['Masks', 'StableAtlas', 'MaskBatch'].includes(feature)) && matrix.masks.failures)
                        throw Error('Enabled mask experiment failed its strict alpha/rounding checks');
                    console.log(`WASM matrix passed: ${files.length} evidence files`);
                }
                if (flags.includes('--matrix-only') || historyOnly) { await context.close(); continue; }
                for (const id of lifecycleOnly || transitionsOnly ? [] : flags.includes('--smoke') ? ['MV01', 'MV10', 'MV30'] : cases) {
                    await select(id);
                    await capture(id);
                }
                // Actual browser input, with state read from the displayed scoped model.
                await select('MV25');
                let state = await read(), viewport = state.viewport;
                const px = Math.round(viewport.x + viewport.width * .45), py = Math.round(viewport.y + viewport.height * .55);
                await page.mouse.move(px, py); await page.mouse.down(); await page.mouse.move(px + 72, py + 48, { steps: 8 }); await page.mouse.up();
                await assert('drag changes camera', s => s.x !== state.x || s.y !== state.y);
                await capture('MV25-drag');
                await page.mouse.move(px + 72, py + 48);
                state = await read(); await page.mouse.wheel(0, -120);
                await page.waitForFunction(zoom => globalThis.openCiv2Snapshot().zoom !== zoom, state.zoom);
                await assert('wheel zoom preserves anchor', s => Math.abs((px + 72 - viewport.x - s.x) / s.zoom - (px + 72 - viewport.x - state.x) / state.zoom) < .01);
                await capture('MV25-wheel');
                await page.mouse.click(px, py); await page.keyboard.press('PageDown');
                await assert('keyboard detail preset', s => s.zoom !== state.zoom);
                await click('overview');
                await page.keyboard.press('ArrowRight'); await page.keyboard.press('Enter');
                await assert('overview keyboard navigation', s => s.overviewFocused && s.overviewX !== null);
                await settle(); state = await read(); await click('recover');
                await page.waitForFunction(sequence => globalThis.openCiv2Snapshot().draw.sequence > sequence, state.draw.sequence);
                await assert('resource recovery preserves view', s => s.draw.error === null && s.x === state.x && s.y === state.y && s.zoom === state.zoom);
                await capture('MV25-reloaded');
                await page.setViewportSize({ width: 1280, height: 900 });
                await wait(() => globalThis.openCiv2Snapshot().viewport.width < 1500);
                await assert('browser resize', s => s.draw.width === s.viewport.width && s.draw.scaling === density);
                await capture('MV25-resized');
                await page.setViewportSize(gallerySize); await settle();
                if (!flags.includes('--smoke')) {
                    await select('MV21');
                    const descriptions = [];
                    const facts = ['Accumulated work 0', 'Accumulated work 9', 'Accumulated work 231', 'Work counter unavailable',
                        'Work counter withheld', 'Population 4', 'Remembered population 5', 'Remembered city site'];
                    for (let i = 0; i < facts.length; i++) {
                        state = await read(); await page.mouse.move(...worldPoint(state, 4 + i * 2, 4));
                        await page.waitForFunction(fact => globalThis.openCiv2Snapshot().inspection.includes(fact), facts[i]);
                        descriptions.push((await read()).inspection);
                    }
                    await page.mouse.click(...worldPoint(await read(), 4, 4));
                    await page.waitForFunction(text => globalThis.openCiv2Snapshot().inspection === text, descriptions[0]);
                    for (const text of descriptions.slice(1)) {
                        await page.keyboard.press('Control+ArrowRight');
                        await page.waitForFunction(text => globalThis.openCiv2Snapshot().inspection === text, text);
                    }
                    await assert('pointer and keyboard expose identical permitted work and city facts', s => s.inspection === descriptions.at(-1));
                    await select('MV09');
                    await page.mouse.click(...worldPoint(await read(), 10, 4));
                    await page.waitForFunction(() => globalThis.openCiv2Snapshot().selectionX === 10);
                    await click('change'); await wait(() => globalThis.openCiv2Snapshot().selectionX === null);
                    await page.mouse.click(...worldPoint(await read(), 4, 4));
                    await page.waitForFunction(() => globalThis.openCiv2Snapshot().selectionX === 4);
                    await page.mouse.click(...worldPoint(await read(), 10, 4));
                    await assert('knowledge region excludes picking and clears stale selection', s => s.selectionX === null && !s.inspection.endsWith('(10, 4)'));
                    await click('change'); await settle();
                    await page.mouse.click(...worldPoint(await read(), 10, 4));
                    await assert('restored knowledge region admits its target', s => s.selectionX === 10);
                    await select('MV17');
                    await page.mouse.click(...worldPoint(await read(), 10, 10));
                    await wait(() => globalThis.openCiv2Snapshot().selectionKind === 'City');
                    state = await read(); await click('change');
                    await page.waitForFunction(revision => globalThis.openCiv2Snapshot().revision > revision, state.revision);
                    await assert('owner role change preserves selected identity', s => s.selectionX === state.selectionX && s.selectionY === state.selectionY && s.selectionKind === 'City');
                    await select('MV19'); state = await read();
                    await click('bareToggle'); await wait(() => globalThis.openCiv2Snapshot().bare && globalThis.openCiv2Snapshot().draw.objects === 0);
                    await click('bareToggle');
                    await assert('bare/full modes restore objects without world changes', s => !s.bare && s.draw.objects === state.draw.objects && s.revision === state.revision);
                    await select('MV24', false); await click('play');
                    await wait(() => { const d = globalThis.openCiv2Snapshot().draw; return d.moving === 1 && d.effects >= 3 && d.notices === 2; });
                    state = await read(); viewport = state.viewport;
                    await page.mouse.move(viewport.x + 350, viewport.y + 250); await page.mouse.down();
                    await page.mouse.move(viewport.x + 374, viewport.y + 250); await page.mouse.up();
                    await assert('navigation during movement and combat preserves world', s => s.x !== state.x && s.revision === state.revision && s.draw.notices === 2);
                    await click('motionToggle');
                    await wait(() => { const s = globalThis.openCiv2Snapshot(); return s.reducedMotion && s.draw.moving === 0 && s.draw.effects === 0; });
                    await assert('reduced motion preserves decision-independent notices', s => s.draw.notices === 2 && s.revision === state.revision);
                    await click('clear'); await wait(() => globalThis.openCiv2Snapshot().draw.notices === 0);
                    await assert('explicit presentation cancellation clears overlays', s => s.draw.effects === 0 && s.draw.moving === 0);
                    await select('MV30');
                    for (const [mode, label] of [['HighContrast', 'high-contrast'], ['Greyscale', 'greyscale'], ['Normal', 'normal-restored']]) {
                        if ((await read()).colorMode !== mode) {
                            await click('display');
                            await page.waitForFunction(mode => globalThis.openCiv2Snapshot().colorMode === mode, mode);
                        }
                        await capture('MV30-' + label);
                        await assert('display mode ' + mode, s => s.colorMode === mode);
                    }
                    await click('change'); await wait(() => globalThis.openCiv2Snapshot().draw.error === 'Unsupported owner color role 17');
                    await assert('unsupported content presents explicit limited frame', s => s.draw.objects === 0);
                    await click('recover'); await assert('retry cannot hide invalid content', s => s.draw.error === 'Unsupported owner color role 17');
                    await click('change'); await wait(() => globalThis.openCiv2Snapshot().draw.error === null);
                    await assert('valid content recovers the browser renderer', s => s.draw.tiles > 0 && s.draw.objects > 0);
                    if (theme !== 'stock') {
                        state = await read(); await click('artToggle'); await wait(() => !globalThis.openCiv2Snapshot().suppliedArt);
                        await click('artToggle'); await wait(() => globalThis.openCiv2Snapshot().suppliedArt);
                        await assert('live art replacement preserves observation and camera', s => s.x === state.x && s.y === state.y && s.zoom === state.zoom && s.revision === state.revision);
                    }
                    if (theme === 'stock' && density === 1) {
                        state = await read();
                        const companionBrowser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE,
                            args: [...(hardware ? ['--enable-gpu', '--ignore-gpu-blocklist', '--disable-software-rasterizer', '--use-angle=gl']
                                : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader']), '--force-device-scale-factor=2'] });
                        const companionContext = await companionBrowser.newContext({ viewport: { width: 840, height: 620 }, deviceScaleFactor: 2 });
                        try {
                            const companion = await companionContext.newPage();
                            await companion.goto(`http://127.0.0.1:${server.address().port}/?showcase=MV23&observe=1${rendererQuery}`);
                            await companion.waitForFunction(() => globalThis.openCiv2Snapshot?.().draw?.tiles > 0, undefined, { timeout: 180000 });
                            const second = await companion.evaluate(() => globalThis.openCiv2Snapshot());
                            if (second.scaling !== 2 || second.case !== 'MV23' || second.draw.composition !== renderer || second.draw.rendererFallback) throw Error('Independent browser map has the wrong density or world');
                            await companion.setViewportSize({ width: 960, height: 720 });
                            await companion.waitForFunction(() => globalThis.openCiv2Snapshot().viewport.width === 960);
                            await assert('simultaneous browser maps retain independent worlds and densities', s => s.scaling === 1 && s.case === state.case && s.x === state.x && s.y === state.y && s.revision === state.revision);
                        } finally { await companionBrowser.close(); }
                        await click('recover');
                        await assert('closing another browser map preserves resource ownership', s => s.draw.error === null && s.x === state.x && s.y === state.y);
                        await page.evaluate(() => {
                            const extension = globalThis.mapWebGlContexts[0].getExtension('WEBGL_lose_context');
                            if (!extension) throw Error('WebGL context-loss injection is unavailable');
                            globalThis.mapContextLossExtension = extension;
                            extension.loseContext();
                        });
                        await page.waitForFunction(() => globalThis.mapContextEvents.includes('lost'));
                        await page.waitForFunction(() => !document.getElementById('error').hidden && globalThis.openCiv2Failure?.includes('graphics connection'));
                        await page.screenshot({ path: path.join(output, 'context-loss.png') });
                        checks.push({ name: 'lost WebGL context presents explicit recovery', theme, density });
                        await page.getByRole('button', { name: 'Reload preview' }).click();
                        await page.waitForFunction(() => !globalThis.openCiv2Failure && globalThis.openCiv2Snapshot?.().draw?.tiles > 0, undefined, { timeout: 180000 });
                        await assert('explicit reload recovers after WebGL context loss', s => s.case === 'MV01' && s.draw.error === null);
                        await click('motionToggle'); await settle();
                        await capture('MV30-context-reopened');
                        const recoveryUrl = page.url();
                        for (const fault of ['manifest', 'license', 'font']) {
                            const leaf = fault === 'manifest' ? 'assets/package.json' : fault === 'license' ? 'assets/LICENSE' : 'fonts/ttf/DejaVuSans.ttf';
                            const pattern = '**/content/' + leaf;
                            await page.route(pattern, async route => {
                                if (fault === 'font') return route.fulfill({ status: 404, body: 'Missing authored test font' });
                                let bytes = fs.readFileSync(path.join(bundle, 'content', leaf));
                                if (fault === 'manifest') { const manifest = JSON.parse(bytes); manifest.version = 'untrusted'; bytes = Buffer.from(JSON.stringify(manifest)); }
                                else { bytes = Buffer.from(bytes); bytes[0] ^= 1; }
                                await route.fulfill({ status: 200, body: bytes });
                            });
                            await page.reload();
                            await page.waitForFunction(() => Boolean(globalThis.openCiv2Failure), undefined, { timeout: 180000 });
                            const failure = await page.evaluate(() => ({ message: globalThis.openCiv2Failure, text: document.getElementById('error').textContent,
                                visible: !document.getElementById('error').hidden, state: globalThis.openCiv2Snapshot?.() }));
                            if (!failure.visible || failure.state?.draw || !failure.message) throw Error('Content failure silently presented a successful map');
                            checks.push({ name: 'content admission rejects ' + fault, density, theme, failure });
                            await page.screenshot({ path: path.join(output, 'admission-' + fault + '.png') });
                            await page.unroute(pattern);
                        }
                        await page.goto(recoveryUrl);
                        await page.waitForFunction(() => globalThis.openCiv2Snapshot?.().draw?.tiles > 0, undefined, { timeout: 180000 });
                        await assert('valid content reload recovers after failed admission', s => s.draw.error === null && s.draw.tiles > 0);
                    }
                }
                await context.close();
            }
        }
        if (errors.length) throw Error(errors.join('\n'));
        persist(false);
        require('node:child_process').execFileSync(process.env.PYTHON || 'python3', ['-c',
            'import json,sys;sys.path.insert(0,"tools/dotnet");from run_application import validate;validate(json.load(open(sys.argv[1]))["identity"])',
            path.join(output, 'browser.json')], { cwd: path.resolve(__dirname, '../..'), stdio: 'inherit' });
        persist(true);
        console.log(`${checks.length} browser checks passed; ${captures.length} captured boards.`);
    } catch (error) {
        if (page && !page.isClosed()) {
            await page.screenshot({ path: path.join(output, 'failure.png'), timeout: 5000 }).catch(() => {});
            fs.writeFileSync(path.join(output, 'failure.json'), JSON.stringify({ error: String(error), state: await page.evaluate(() => globalThis.openCiv2Snapshot?.()).catch(() => null), checks, errors }, null, 2));
            const files = await page.evaluate(() => globalThis.openCiv2ResultFiles?.()).catch(() => []);
            for (const file of files || []) {
                const encoded = await page.evaluate(file => {
                    const bytes = globalThis.openCiv2ResultFile(file);
                    let binary = ''; for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
                    return btoa(binary);
                }, file);
                const destination = path.join(output, 'wasm', file.slice('/results/'.length));
                fs.mkdirSync(path.dirname(destination), { recursive: true });
                fs.writeFileSync(destination, Buffer.from(encoded, 'base64'));
            }
        }
        throw error;
    } finally {
        if (browser) await browser.close();
        server.closeAllConnections(); await new Promise(resolve => server.close(resolve));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
