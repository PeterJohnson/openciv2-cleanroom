// Input-only batch host. Fixture expectations and comparisons remain in Python.
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');
const version = require('playwright/package.json').version;
const expectedVersion = require('../../dependencies.json').tools.playwright;

async function main() {
    if (version !== expectedVersion) throw new Error(`Expected Playwright ${expectedVersion}; got ${version}`);
    const [bundleArg, inputsArg, outputArg] = process.argv.slice(2);
    if (!bundleArg || !inputsArg || !outputArg) throw new Error('Usage: node run_conformance_browser.cjs BUNDLE INPUTS OUTPUT');
    const bundle = path.resolve(bundleArg);
    const requests = JSON.parse(fs.readFileSync(inputsArg, 'utf8'));
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (pathname === '/' ? '/index.html' : pathname));
            if (!file.startsWith(bundle + path.sep)) throw new Error('Outside bundle');
            const bytes = fs.readFileSync(file);
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream' });
            response.end(bytes);
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    let browser;
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE });
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(String(error)));
        await page.goto(`http://127.0.0.1:${server.address().port}/`, { timeout: 60000 });
        await page.waitForFunction(() => globalThis.conformanceAdapter || globalThis.conformanceFailure, undefined, { timeout: 60000 });
        const identity = await page.evaluate(() => {
            if (globalThis.conformanceFailure) throw new Error(globalThis.conformanceFailure);
            return JSON.parse(globalThis.conformanceAdapter.Identity());
        });
        const observations = [];
        for (const request of requests) {
            let timer;
            try {
                const observation = await Promise.race([
                    page.evaluate(request => JSON.parse(globalThis.conformanceAdapter.Run(JSON.stringify(request.arguments), request.input)), request),
                    new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Adapter invocation watchdog expired')), request.hostMilliseconds); })
                ]);
                observations.push(observation);
            } finally { clearTimeout(timer); }
        }
        fs.mkdirSync(path.dirname(path.resolve(outputArg)), { recursive: true });
        fs.writeFileSync(outputArg, JSON.stringify({ identity, observations, browser: browser.version(), playwright: version, pageErrors: errors }, null, 2) + '\n');
        if (errors.length) throw new Error(`Browser execution errors: ${errors.join('; ')}`);
        console.log(`${observations.length} managed conformance invocations completed (${browser.version()}).`);
    } finally {
        if (browser) await browser.close();
        await new Promise(resolve => server.close(resolve));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
