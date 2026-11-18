// Runs the same specification/policy cases as the native xUnit suite.
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');
const version = require('playwright/package.json').version;
const expectedVersion = require('../../dependencies.json').tools.playwright;

async function main() {
    if (version !== expectedVersion) throw new Error(`Expected Playwright ${expectedVersion}; got ${version}`);
    const [bundleArg, reportArg] = process.argv.slice(2);
    if (!bundleArg || !reportArg) throw new Error('Usage: node run_browser.cjs BUNDLE REPORT.json');
    const bundle = path.resolve(bundleArg);
    const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    const server = http.createServer((request, response) => {
        try {
            const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
            const file = path.resolve(bundle, '.' + (pathname === '/' ? '/index.html' : pathname));
            if (!file.startsWith(bundle + path.sep)) throw new Error('Outside bundle');
            const bytes = fs.readFileSync(file);
            response.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream' });
            response.end(bytes);
        } catch {
            response.writeHead(404);
            response.end();
        }
    });
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    let browser;
    const errors = [];
    const messages = [];
    const failedRequests = [];
    const started = Date.now();
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE });
        const page = await browser.newPage();
        page.on('pageerror', error => errors.push(String(error)));
        page.on('console', message => messages.push({ type: message.type(), text: message.text() }));
        page.on('requestfailed', request => failedRequests.push({ url: request.url(), error: request.failure() }));
        await page.goto(`http://127.0.0.1:${server.address().port}/`, { timeout: 60000 });
        // The complete 432-group interpreter suite takes about 50 seconds on
        // the reference workstation. This correctness timeout admits slower CI
        // CPUs; startup/workload performance budgets are measured separately.
        await page.waitForFunction(() => globalThis.foundationResults !== undefined, undefined, { timeout: 180000 });
        const report = await page.evaluate(() => globalThis.foundationResults);
        report.Browser = browser.version();
        report.Playwright = version;
        report.PageErrors = errors;
        fs.mkdirSync(path.dirname(path.resolve(reportArg)), { recursive: true });
        fs.writeFileSync(reportArg, JSON.stringify(report, null, 2) + '\n');
        if (!report.Passed || errors.length || !report.Cases?.length)
            throw new Error(`Foundation checks failed: ${JSON.stringify(report)}`);
        console.log(`${report.Cases.length} browser foundation cases passed (${report.Browser}).`);
    } catch (error) {
        const diagnostics = { Format: 'openciv2-browser-run-failure/1', Error: String(error),
            ElapsedMs: Date.now() - started, Browser: browser?.version(), Playwright: version,
            PageErrors: errors, ConsoleMessages: messages, FailedRequests: failedRequests };
        fs.mkdirSync(path.dirname(path.resolve(reportArg)), { recursive: true });
        fs.writeFileSync(reportArg + '.failure.json', JSON.stringify(diagnostics, null, 2) + '\n');
        throw error;
    } finally {
        if (browser) await browser.close();
        await new Promise(resolve => server.close(resolve));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
