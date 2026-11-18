// Runs the production Lua/session cases as the native probe.
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');
const version = require('playwright/package.json').version;
const expectedVersion = require('../../dependencies.json').tools.playwright;

async function main() {
    if (version !== expectedVersion) throw new Error(`Expected Playwright ${expectedVersion}; got ${version}`);
    const [bundleArg, reportArg] = process.argv.slice(2);
    if (!bundleArg || !reportArg) throw new Error('Usage: node run_automation_browser.cjs BUNDLE REPORT.json');
    const bundle = path.resolve(bundleArg);
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (pathname === '/' ? '/index.html' : pathname));
            if (!file.startsWith(bundle + path.sep)) throw new Error('Outside bundle');
            const bytes = fs.readFileSync(file);
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream',
                'Cross-Origin-Opener-Policy': 'same-origin', 'Cross-Origin-Embedder-Policy': 'require-corp' });
            response.end(bytes);
        } catch {
            response.writeHead(404);
            response.end();
        }
    });
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    let browser;
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE });
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(String(error)));
        await page.goto(`http://127.0.0.1:${server.address().port}/`, { timeout: 60000 });
        await page.waitForFunction(() => globalThis.automationResults !== undefined, undefined, { timeout: 60000 });
        const report = await page.evaluate(() => globalThis.automationResults);
        report.Browser = browser.version();
        report.Playwright = version;
        report.PageErrors = errors;
        fs.mkdirSync(path.dirname(path.resolve(reportArg)), { recursive: true });
        fs.writeFileSync(reportArg, JSON.stringify(report, null, 2) + '\n');
        if (!report.Passed || errors.length || !report.Cases?.length)
            throw new Error(`Lua checks failed: ${JSON.stringify({ Error: report.Error, Cases: report.Cases?.filter(c => !c.Passed), PageErrors: errors })}`);
        console.log(`${report.Cases.length} browser Lua cases passed (${report.Browser}).`);
    } finally {
        if (browser) await browser.close();
        await new Promise(resolve => server.close(resolve));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
