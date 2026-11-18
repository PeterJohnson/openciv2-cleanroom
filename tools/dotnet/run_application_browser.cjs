// Real pointer/keyboard input into the production Avalonia browser host.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const { chromium } = require('playwright');
async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright) throw new Error('Unpinned Playwright');
    const [bundleArg, outputArg] = process.argv.slice(2);
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
            browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', `--force-device-scale-factor=${scale}`] });
            browserVersion = browser.version();
            const context = await browser.newContext({ viewport: { width: 1000, height: 800 }, deviceScaleFactor: scale });
            const page = await context.newPage(); page.on('pageerror', error => errors.push(String(error)));
            await page.goto(`http://127.0.0.1:${server.address().port}/`);
            await page.waitForFunction(() => globalThis.openCiv2Failure || globalThis.openCiv2Snapshot?.().controls?.send?.width > 0, undefined, { timeout: 90000 });
            if (await page.evaluate(() => globalThis.openCiv2Failure)) throw new Error(await page.evaluate(() => globalThis.openCiv2Failure));
            const read = () => page.evaluate(() => globalThis.openCiv2Snapshot());
            const check = async (name, test) => { const state = await read(); if (!test(state)) throw new Error(`${scale}: ${name}: ${JSON.stringify(state)}`); checks.push({ scale, name, state }); };
            const wait = async status => { try { await page.waitForFunction(status => globalThis.openCiv2Snapshot().status === status, status); } catch (error) { await page.screenshot({ path: output + '.failure.png' }); throw new Error(error + ': ' + JSON.stringify(await read())); } };
            const click = async name => { const box = (await read()).controls[name]; if (!box.enabled) throw new Error(`${name} disabled`); await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2); };
            await check('viewport-and-density', s => Math.abs(s.width - 1000) < 2 && Math.abs(s.height - 800) < 2 && Math.abs(s.scaling - scale) < .01);
            identity = await page.evaluate(() => globalThis.openCiv2Identity());
            await check('observer-filtered-units', s => s.observer === '1' && s.units.map(u => u.slot).join(',') === '0,1,2');
            await page.screenshot({ path: output + '.initial.png' }); await click('send'); await wait('AwaitingInteraction'); await check('asynchronous-decision', s => s.effects === 0 && s.controls.confirm.enabled && !s.controls.send.enabled);
            await click('cancel'); await wait('Cancelled'); await check('cancel-preserves-state', s => s.effects === 0);
            await click('input'); await page.keyboard.press('ControlOrMeta+A'); await page.keyboard.type('unit.stack-bottom unit=0:100'); await page.keyboard.press('Enter'); await wait('AwaitingInteraction');
            // Input retains focus; the next enabled control is the confirmation button.
            await page.keyboard.press('Tab'); await page.keyboard.press('Space'); await wait('Completed');
            await check('console-and-keyboard-confirm', s => s.effects === 1 && s.changed === true);
            await page.screenshot({ path: output + `.scale-${scale}.png` });
            await click('send'); await wait('AwaitingInteraction'); await click('confirm'); await wait('Completed'); await check('repeat-is-no-op', s => s.changed === false && s.effects === 0);
            await click('send'); await wait('AwaitingInteraction'); await click('decline'); await wait('Cancelled'); await check('decline', s => s.effects === 0);
            await click('input'); await page.keyboard.press('ControlOrMeta+A'); await page.keyboard.type('unit.stack-bottom unit=3:103'); await click('run'); await wait('Rejected');
            await check('foreign-target-rejected', s => s.message.includes('unit_not_available') && s.effects === 0 && s.units.length === 3);
            await context.close(); await browser.close(); browser = null;
        }
        if (errors.length) throw new Error(errors.join('\n'));
        fs.writeFileSync(output, JSON.stringify({ format: 'openciv2-application-input/1', browser: browserVersion, identity, checks, errors }, null, 2) + '\n');
        console.log(`${checks.length} Avalonia browser input checks passed.`);
    } finally { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
