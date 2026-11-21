// Real pointer/keyboard input into the production Avalonia browser host.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const { chromium } = require('playwright');
async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright) throw new Error('Unpinned Playwright');
    const [bundleArg, outputArg, ...flags] = process.argv.slice(2);
    const renderer = flags.find(f => f.startsWith('--renderer='))?.split('=')[1] ?? 'cpu';
    if (!['cpu', 'gpu'].includes(renderer)) throw Error('Unknown renderer');
    const hardware = flags.includes("--hardware");
    const gpuArgs = hardware ? ["--enable-gpu", "--ignore-gpu-blocklist", "--disable-software-rasterizer", "--use-angle=gl"] : ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"];
    if (!bundleArg || !outputArg) throw new Error('Usage: run_application_browser.cjs BUNDLE OUTPUT');
    const bundle = path.resolve(bundleArg), output = path.resolve(outputArg);
    fs.mkdirSync(path.dirname(output), { recursive: true });
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (name === '/' ? '/index.html' : name));
            if (!file.startsWith(bundle + path.sep)) throw new Error('Outside bundle');
            const data = fs.readFileSync(file);
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' }); response.end(data);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    const checks = [], errors = [];
    try {
        let identity, browserVersion;
        for (const scale of [1, 2]) {
            browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE, args: [...gpuArgs, `--force-device-scale-factor=${scale}`] });
            browserVersion = browser.version();
            const context = await browser.newContext({ viewport: { width: 1000, height: 800 }, deviceScaleFactor: scale });
            const page = await context.newPage(); page.on('pageerror', error => errors.push(String(error)));
            await page.goto(`http://127.0.0.1:${server.address().port}/${renderer === 'gpu' ? '?renderer=gpu' : ''}`);
            await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().controls?.send?.width > 0, undefined, { timeout: 90000 });
            if (await page.evaluate(() => globalThis.openCiv2Failure)) throw new Error(await page.evaluate(() => globalThis.openCiv2Failure));
            const read = () => page.evaluate(() => globalThis.openCiv2Snapshot());
            const check = async (name, test) => { const state = await read(); if (!test(state)) throw new Error(`${scale}: ${name}: ${JSON.stringify(state)}`); checks.push({ scale, name, state }); };
            const wait = async status => { try { await page.waitForFunction(status => globalThis.openCiv2Snapshot().status === status, status); } catch (error) { await page.screenshot({ path: output + '.failure.png' }); throw new Error(error + ': ' + JSON.stringify(await read())); } };
            const click = async name => {
                // Model snapshots can expose a new dialog before the canvas hit-test tree updates.
                // Allow a complete rendering turn, then read the current control bounds.
                await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
                const box = (await read()).controls[name];
                if (!box.enabled) throw new Error(`${name} disabled`);
                await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
            };
            await check('viewport-and-density', s => Math.abs(s.width - 1000) < 2 && Math.abs(s.height - 800) < 2 && Math.abs(s.scaling - scale) < .01);
            await page.waitForFunction(() => globalThis.openCiv2Snapshot()?.map?.composition);
            await check('startup-renderer', s => s.map.composition === renderer && s.map.requestedRenderer === renderer && !s.map.rendererFallback);
            identity = await page.evaluate(() => globalThis.openCiv2Identity());
            await check('observer-filtered-units', s => s.observer === '1' && s.units.map(u => u.slot).join(',') === '0,1,2');
            await page.screenshot({ path: output + '.initial.png' }); await click('send'); await wait('AwaitingInteraction'); await check('asynchronous-decision', s => s.effects === 0 && s.controls.confirm.enabled && !s.controls.send.enabled);
            await click('cancel'); await wait('Cancelled'); await check('cancel-preserves-state', s => s.effects === 0);
            await click('input'); await page.keyboard.press('ControlOrMeta+A'); await page.keyboard.type('unit.stack-bottom unit=0:100'); await page.keyboard.press('Enter'); await wait('AwaitingInteraction');
            // Opening the dialog focuses Confirm; Tab would move away from it.
            await page.keyboard.press('Space'); await wait('Completed');
            await check('console-and-keyboard-confirm', s => s.effects === 1 && s.changed === true);
            await page.screenshot({ path: output + `.scale-${scale}.png` });
            await click('send'); await wait('AwaitingInteraction'); await click('confirm'); await wait('Completed'); await check('repeat-is-no-op', s => s.changed === false && s.effects === 0);
            await click('send'); await wait('AwaitingInteraction'); await click('decline'); await wait('Cancelled'); await check('decline', s => s.effects === 0);
            await click('input'); await page.keyboard.press('ControlOrMeta+A'); await page.keyboard.type('unit.stack-bottom unit=3:103'); await click('run'); await wait('Rejected');
            await check('foreign-target-rejected', s => s.message.includes('unit_not_available') && s.effects === 0 && s.units.length === 3);
            const settle = () => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
            const menuAction = async (group, action) => {
                await settle();
                const header = (await read()).menu.groups[group].bounds;
                await page.mouse.click(header.x + header.width / 2, header.y + header.height / 2);
                await page.waitForFunction(group => globalThis.openCiv2Snapshot().menu.groups[group].open, group);
                await settle();
                const item = (await read()).menu.actions[action].bounds;
                if (!item?.enabled || !item.width) {
                    await page.screenshot({ path: output + '.menu-failure.png' });
                    throw Error('Menu action unavailable: ' + action + ': ' + JSON.stringify(await read()));
                }
                await page.mouse.click(item.x + item.width / 2, item.y + item.height / 2);
            };
            const initialDispatches = (await read()).menu.dispatches;
            await menuAction('Orders', 'unit.stack-bottom'); await wait('AwaitingInteraction');
            await check('menu-action-dialog', s => s.menu.dispatches === initialDispatches + 1 && s.menu.lastAction === 'unit.stack-bottom' &&
                s.dialog.visible && !s.menu.groups.Orders.open && !s.menu.actions['unit.stack-bottom'].enabled);
            await settle();
            await check('custom-choice-regions', s => ['choice.1', 'choice.0', 'cancel'].every(id => {
                const b = s.dialog.regions[id]; return b.enabled && b.width > 60 && b.height > 20 && b.x >= 0 && b.y >= 0 && b.x + b.width <= s.width && b.y + b.height <= s.height;
            }) && s.dialog.regions['choice.1'].focused && s.dialog.target.includes('Unit 0'));
            await page.screenshot({ path: output + `.dialog-${scale}.png` });
            await page.keyboard.press('Tab'); await page.keyboard.press('Tab'); await page.keyboard.press('Tab');
            await page.keyboard.press('ControlOrMeta+Enter'); await page.keyboard.press('ControlOrMeta+g');
            await page.mouse.click(5, 5);
            await check('dialog-focus-cycle-and-isolation', s => s.dialog.visible && s.dialog.regions['choice.1'].focused &&
                s.menu.dispatches === initialDispatches + 1 && !s.map.grid && !s.controls.mapOptions.enabled);
            await page.keyboard.press('Escape'); await wait('Cancelled');
            await check('dialog-cancel-focus-return', s => !s.dialog.visible && s.controls.map.focused && s.effects === 0);
            const modifier = process.platform === 'darwin' ? 'Meta' : 'Control';
            await page.keyboard.down(modifier); await page.keyboard.down('Enter'); await page.keyboard.down('Enter');
            await page.keyboard.up('Enter'); await page.keyboard.up(modifier); await wait('AwaitingInteraction');
            await page.keyboard.press('Space'); await wait('Completed');
            await check('central-shortcut-exactly-once', s => s.menu.dispatches === initialDispatches + 2 && s.changed === false && s.effects === 0);
            await menuAction('View', 'view.grid');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().map.grid);
            if (!(await read()).menu.actions['view.grid'].checked) throw Error('Menu check state is stale');
            // Selecting a menu item already closes the popup and restores map focus.
            await page.keyboard.down(modifier); await page.keyboard.down('g'); await page.keyboard.down('g');
            await page.keyboard.up('g'); await page.keyboard.up(modifier);
            await page.waitForFunction(() => !globalThis.openCiv2Snapshot().map.grid);
            await check('menu-checked-and-shortcut-repeat', s => s.menu.dispatches === initialDispatches + 4 && !s.menu.actions['view.grid'].checked);
            for (const [action, field] of [['view.bare', 'bare'], ['view.reduced-motion', 'reducedMotion']]) {
                for (const enabled of [true, false]) {
                    await menuAction('View', action);
                    await page.waitForFunction(({ action, field, enabled }) => {
                        const s = globalThis.openCiv2Snapshot(); return s.map[field] === enabled && s.menu.actions[action].checked === enabled;
                    }, { action, field, enabled });
                }
            }
            await menuAction('View', 'view.map-options');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().controls.modeToggles.grid?.width > 0);
            for (const enabled of [true, false]) {
                await settle(); const b = (await read()).controls.modeToggles.grid;
                await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);
                await page.waitForFunction(enabled => {
                    const s = globalThis.openCiv2Snapshot(); return s.map.grid === enabled && s.menu.actions['view.grid'].checked === enabled;
                }, enabled);
            }
            await page.keyboard.press('Escape');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().controls.modeToggles.grid === null);
            await menuAction('View', 'view.console');
            await check('menu-preferences-and-console', s => s.inputFocused && !s.map.bare && !s.map.reducedMotion && !s.map.grid &&
                s.menu.dispatches === initialDispatches + 10);
            await click('input'); await page.keyboard.press('ControlOrMeta+g');
            await check('text-input-shortcut-isolation', s => s.inputFocused && !s.map.grid && s.menu.dispatches === initialDispatches + 10);
            await page.setViewportSize({ width: 680, height: 640 });
            await menuAction('Orders', 'unit.stack-bottom'); await wait('AwaitingInteraction'); await settle();
            await page.screenshot({ path: output + `.dialog-small-${scale}.png` });
            const small = await read();
            if (Object.values(small.dialog.regions).some(b => b.x < 0 || b.y < 0 || b.x + b.width > small.width || b.y + b.height > small.height))
                throw Error('Resized dialog hit region outside viewport');
            await click('cancel'); await wait('Cancelled');
            await check('resized-dialog-and-cancel', s => !s.dialog.visible && s.effects === 0 && s.menu.dispatches === initialDispatches + 11);
            await page.keyboard.press('Alt+o');
            await page.waitForFunction(() => globalThis.openCiv2Snapshot().menu.groups.Orders.open);
            await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter'); await wait('AwaitingInteraction');
            await page.keyboard.press('Escape'); await wait('Cancelled');
            await check('keyboard-menu-route', s => s.menu.dispatches === initialDispatches + 12 && s.effects === 0 && s.controls.map.focused);
            await context.close(); await browser.close(); browser = null;
        }
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE, args: gpuArgs });
        for (const [query, expected] of [['renderer=cpu', 'cpu'], ['backend=software', 'cpu'], ['backend=software&renderer=gpu', 'rejected']]) {
            const context = await browser.newContext({ viewport: { width: 1000, height: 800 } });
            const page = await context.newPage();
            await page.goto(`http://127.0.0.1:${server.address().port}/?${query}`);
            await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().map?.composition, undefined, { timeout: 180000 });
            const state = await page.evaluate(() => ({ failure: globalThis.openCiv2Failure, map: globalThis.openCiv2Snapshot?.().map }));
            if (expected === 'rejected' ? !state.failure?.includes('requires the WebGL') : state.failure || state.map.composition !== expected || state.map.rendererFallback)
                throw Error(`Renderer configuration ${query}: ${JSON.stringify(state)}`);
            checks.push({ name: 'renderer-configuration', query, expected, state });
            await context.close();
        }
        if (errors.length) throw new Error(errors.join('\n'));
        fs.writeFileSync(output, JSON.stringify({ format: 'openciv2-application-input/1', browser: browserVersion, hardware, renderer, identity, checks, errors }, null, 2) + '\n');
        console.log(`${checks.length} Avalonia browser input checks passed.`);
    } finally { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
