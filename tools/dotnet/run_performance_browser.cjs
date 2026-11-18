// Execute production workloads in the real published Release WASM runtime.
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');
const observeWasmMemory = require('./observe_wasm_memory.cjs');
const version = require('playwright/package.json').version;

async function main() {
    if (version !== require('../../dependencies.json').tools.playwright) throw new Error('Unpinned Playwright');
    const [bundleArg, inputsArg, reportArg, repetitionsArg] = process.argv.slice(2);
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
        } catch { response.writeHead(404); response.end(); }
    });
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    let browser;
    try {
        browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE });
        const page = await browser.newPage(); const errors = [];
        page.on('pageerror', error => errors.push(String(error)));
        await page.addInitScript(observeWasmMemory);
        await page.goto(`http://127.0.0.1:${server.address().port}/`, { timeout: 60000 });
        await page.waitForFunction(() => globalThis.runWorkloads || globalThis.workloadsError, undefined, { timeout: 60000 });
        const report = await page.evaluate(({ inputs, repetitions }) => {
            if (globalThis.workloadsError) throw new Error(globalThis.workloadsError);
            return globalThis.runWorkloads(inputs, repetitions);
        }, { inputs: fs.readFileSync(inputsArg, 'utf8'), repetitions: Number(repetitionsArg) });
        report.WasmMemoryAfter = await page.evaluate(() => globalThis.oc2WasmCapacity());
        if (!report.WasmMemoryAfter.memories || report.WasmMemoryAfter.bytes <= 0) throw new Error('Runtime WASM memory was not observed');
        report.Browser = browser.version(); report.Playwright = version; report.PageErrors = errors;
        if (errors.length) throw new Error(errors.join('\n'));
        fs.writeFileSync(reportArg, JSON.stringify(report));
        console.log('Completed production WASM workload samples.');
    } finally {
        if (browser) await browser.close();
        await new Promise(resolve => server.close(resolve));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
