// Real WASM shell input and persistence, with faults injected only at the browser storage boundary.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), os = require('node:os');
const { chromium } = require('playwright');
const crypto = require('node:crypto');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright) throw Error('Unpinned Playwright');
    const [bundleArg, outputArg] = process.argv.slice(2);
    if (!bundleArg || !outputArg) throw Error('Usage: run_storage_browser.cjs BUNDLE OUTPUT');
    const bundle = path.resolve(bundleArg), output = path.resolve(outputArg);
    fs.mkdirSync(path.dirname(output), { recursive: true });
    const server = http.createServer((request, response) => {
        try {
            const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (name === '/' ? '/index.html' : name));
            if (!file.startsWith(bundle + path.sep)) throw Error('Outside bundle');
            const data = fs.readFileSync(file);
            response.writeHead(200, { 'Content-Type': ({ '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.html': 'text/html' })[path.extname(file)] || 'application/octet-stream', 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
            response.end(data);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const origin = `http://127.0.0.1:${server.address().port}/`;
    const inventory = directory => Object.fromEntries(fs.readdirSync(directory, { recursive: true, withFileTypes: true })
        .filter(entry => entry.isFile()).map(entry => { const file = path.join(entry.parentPath, entry.name); return [file, hash(fs.readFileSync(file))]; }).sort());
    const artifacts = inventory(bundle), driverSha256 = hash(fs.readFileSync(__filename));
    const checks = [], errors = [], identities = [];
    let context, page, browserVersion;
    const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'oc2-storage-browser-'));
    try {
        for (const scale of [1, 2]) {
            const launch = async () => {
                context = await chromium.launchPersistentContext(path.join(profile, String(scale)), {
                    executablePath: process.env.OC2_CHROMIUM_EXECUTABLE, headless: true,
                    viewport: { width: 1000, height: 800 }, deviceScaleFactor: scale,
                    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', `--force-device-scale-factor=${scale}`]
                });
                browserVersion = context.browser().version();
                page = context.pages()[0]; page.setDefaultTimeout(30000); page.on('pageerror', error => errors.push(String(error)));
                await page.goto(origin);
                await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().files, undefined, { timeout: 90000 });
                if (await page.evaluate(() => globalThis.openCiv2Failure)) throw Error(await page.evaluate(() => globalThis.openCiv2Failure));
                identities.push(await page.evaluate(() => globalThis.openCiv2Identity()));
            };
            const read = () => page.evaluate(() => globalThis.openCiv2Snapshot());
            const settle = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
            const clickBox = async box => { if (!box?.enabled || !box.width) throw Error('Invalid control'); await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2); };
            const click = async id => { await settle(); await clickBox((await read()).files.controls[id]); };
            const action = async (group, id) => {
                await settle(); await clickBox({ ...(await read()).menu.groups[group].bounds, enabled: true });
                await page.waitForFunction(group => globalThis.openCiv2Snapshot().menu.groups[group].open, group); await settle();
                await clickBox((await read()).menu.actions[id].bounds);
            };
            const open = async () => { await action('File', 'file.library'); await page.waitForFunction(() => {
                const s = globalThis.openCiv2Snapshot(); return s.files.visible && !s.files.busy && s.files.controls.import.width > 0 && s.files.controls.import.focused;
            }); };
            const waitIdle = () => page.waitForFunction(() => !globalThis.openCiv2Snapshot().files.busy);
            const check = async (name, predicate) => { const state = await read(); if (!predicate(state)) throw Error(`${name}: ${JSON.stringify(state)}`); checks.push({ scale, name, state }); console.log(`${scale}x: ${name}`); };
            await launch();
            await check('viewport-and-density', s => s.width === 1000 && s.height === 800 && s.scaling === scale);
            await action('View', 'view.grid'); await open();
            await check('file-panel-focus-and-empty-library', s => s.files.entries.length === 0 && !s.controls.send.enabled && s.files.controls.import.focused);
            // The OS chooser is intercepted by Playwright, while application input and File bytes are real.
            let chooserPromise = page.waitForEvent('filechooser'); await click('import'); let chooser = await chooserPromise;
            const ticks = (await read()).serviceTicks;
            await page.waitForFunction(ticks => globalThis.openCiv2Snapshot().serviceTicks >= ticks + 3, ticks);
            await check('pending-picker-services-responsive', s => s.files.busy && s.files.canCancel && !s.files.controls.close.enabled);
            await click('cancel'); await waitIdle();
            // A selected stale HTML input cannot publish after cancellation.
            await chooser.setFiles({ name: 'late.sav', mimeType: 'application/octet-stream', buffer: Buffer.from([9]) }).catch(() => {});
            await check('cancel-and-late-picker-reply', s => s.files.entries.length === 0 && s.files.message === 'Import cancelled.');
            const payload = Buffer.from(Array.from({ length: 131077 }, (_, i) => i % 256));
            chooserPromise = page.waitForEvent('filechooser'); await click('import'); chooser = await chooserPromise;
            await chooser.setFiles({ name: '存档 É.SAV', mimeType: 'application/octet-stream', buffer: payload }); await waitIdle();
            await check('import-committed-once', s => s.files.entries.length === 1 && s.files.entries[0].name === '存档 É.SAV' && s.files.entries[0].size === payload.length && s.files.message.startsWith('Stored '));
            await click('preferences'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.message === 'Display preferences saved.');
            await page.screenshot({ path: output + `.files-${scale}.png` });
            await context.close(); context = null; await launch();
            await check('preferences-survive-browser-restart', s => s.map.grid);
            await open(); await check('file-survives-browser-restart', s => s.files.entries.length === 1 && s.files.entries[0].size === payload.length);
            const downloadPromise = page.waitForEvent('download'); await click('export'); const download = await downloadPromise;
            const exported = output + `.export-${scale}.sav`; await download.saveAs(exported); await waitIdle();
            if (download.suggestedFilename() !== '存档 É.SAV' || !fs.readFileSync(exported).equals(payload)) throw Error('Export bytes/name changed');
            await check('byte-exact-download-with-truthful-status', s => s.files.message.startsWith('Download requested.'));
            // Abort after put success, proving persistence waits for tx completion, not IDBRequest success.
            await page.evaluate(() => {
                globalThis.originalPut = IDBObjectStore.prototype.put;
                IDBObjectStore.prototype.put = function (...args) {
                    const request = globalThis.originalPut.apply(this, args), tx = this.transaction;
                    request.addEventListener('success', () => tx.abort()); return request;
                };
            });
            chooserPromise = page.waitForEvent('filechooser'); await click('import'); chooser = await chooserPromise;
            await chooser.setFiles({ name: 'aborted.sav', mimeType: 'application/octet-stream', buffer: Buffer.from([7]) }); await waitIdle();
            await check('transaction-abort-never-reports-saved', s => s.files.entries.length === 1 && s.files.message.startsWith('File operation failed:'));
            await page.evaluate(() => { IDBObjectStore.prototype.put = globalThis.originalPut; });
            // Try saving a changed value; a rejected write must preserve the previous preference on restart.
            await click('close'); await action('View', 'view.grid'); await open();
            // Quota/permission failures use the same rejected native API boundary, without test switches in production.
            await page.evaluate(() => { IDBObjectStore.prototype.put = function () { throw new DOMException('Authored quota limit', 'QuotaExceededError'); }; });
            await click('preferences');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.message.startsWith('Could not save display preferences:'));
            await check('quota-error-visible', s => s.files.message.includes('Authored quota limit'));
            await page.evaluate(() => { IDBObjectStore.prototype.put = globalThis.originalPut; });
            await click('remove'); await waitIdle(); await check('remove-completes', s => s.files.entries.length === 0);
            await page.reload(); await page.waitForFunction(() => globalThis.openCiv2Snapshot?.().files, undefined, { timeout: 90000 });
            await open(); await check('remove-survives-reload', s => s.files.entries.length === 0);
            await check('failed-preference-retains-previous-value', s => s.map.grid);
            await page.setViewportSize({ width: 680, height: 640 }); await settle();
            await check('resized-panel-controls', s => ['import', 'preferences', 'close'].every(id => { const b = s.files.controls[id]; return b.x >= 0 && b.y >= 0 && b.x + b.width <= s.width && b.y + b.height <= s.height; }));
            await page.screenshot({ path: output + `.small-${scale}.png` });
            await click('close'); await page.waitForFunction(() => !globalThis.openCiv2Snapshot().files.visible);
            await check('focus-return', s => s.controls.map.focused);
            await clickBox((await read()).controls.send);
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().status === 'AwaitingInteraction'); await settle();
            await clickBox((await read()).controls.confirm);
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().status === 'Completed');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().map.notices > 0);
            await check('silent-media-semantic-completion', s => s.effects === 1 && s.media === 'Skipped');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().map.notices === 0);
            await check('presentation-clock-expiry', s => s.effects === 1 && s.status === 'Completed');
            // Browser renderer persistence uses the same asynchronous store and UI as native settings.
            await action('View', 'view.map-options'); await settle();
            for (let tab = 0; tab < 30 && !(await read()).rendererPreferences.focused; tab++) await page.keyboard.press('Tab');
            if (!(await read()).rendererPreferences.focused) throw Error('Renderer preference keyboard focus unavailable');
            await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().rendererPreferences.saved === 'Gpu');
            await check('renderer-preference-commits-asynchronously', s => s.rendererPreferences.message.startsWith('Saved.') && s.map.requestedRenderer === 'cpu');
            await page.keyboard.press('Escape');
            await page.goto(origin); await page.waitForFunction(() => globalThis.openCiv2Snapshot?.().map?.composition, undefined, { timeout: 90000 });
            await check('saved-renderer-applies-after-reload', s => s.rendererPreferences.saved === 'Gpu' && s.map.requestedRenderer === 'gpu');
            // Explicit CPU override must not rewrite the saved GPU preference.
            await page.goto(origin + '?renderer=cpu'); await page.waitForFunction(() => globalThis.openCiv2Snapshot?.().map?.composition, undefined, { timeout: 90000 });
            await check('renderer-override-preserves-saved-choice', s => s.rendererPreferences.saved === 'Gpu' && s.map.requestedRenderer === 'cpu');
            await context.close(); context = null;
        }
        if (errors.length) throw Error(errors.join('\n'));
        if (JSON.stringify(artifacts) !== JSON.stringify(inventory(bundle))) throw Error('Browser artifacts changed during validation');
        fs.writeFileSync(output, JSON.stringify({ format: 'openciv2-storage-input/1', browser: browserVersion, driverSha256, invokedArtifacts: artifacts, checks, identities, errors }, null, 2) + '\n');
        console.log(`Passed ${checks.length} browser storage checks.`);
    } catch (error) {
        if (page && !page.isClosed()) { await page.screenshot({ path: output + '.failure.png' }).catch(() => {}); fs.writeFileSync(output + '.failure.json', JSON.stringify(await page.evaluate(() => globalThis.openCiv2Snapshot?.()).catch(() => null), null, 2)); }
        throw error;
    } finally { await context?.close(); await new Promise(resolve => server.close(resolve)); fs.rmSync(profile, { recursive: true, force: true }); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
