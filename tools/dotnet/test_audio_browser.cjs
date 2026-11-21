// Standalone service-boundary tests of the production JS module, using actual Web Audio.
const fs = require('node:fs'), http = require('node:http'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require('playwright');
(async () => {
    const [bundle, report] = process.argv.slice(2), source = fs.readFileSync(path.join(bundle, 'audio.js'));
    const server = http.createServer((req, res) => { res.setHeader('Content-Type', req.url === '/audio.js' ? 'text/javascript' : 'text/html'); res.end(req.url === '/audio.js' ? source : '<button>Enable audio</button>'); });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser; const checks = [];
    try {
        browser = await chromium.launch({ executablePath: process.env.OC2_CHROMIUM_EXECUTABLE,
            args: ['--autoplay-policy=document-user-activation-required'], headless: true });
        const page = await browser.newPage();
        await page.goto(`http://127.0.0.1:${server.address().port}/`);
        const cdp = await page.context().newCDPSession(page);
        await cdp.send('Runtime.evaluate', { expression: `(async () => {
            globalThis.output = await import('/audio.js');
            const bytes = new Uint8Array(800 * 4), data = new DataView(bytes.buffer);
            for (let i = 0; i < 800; i++) data.setFloat32(i * 4, Math.sin(i * Math.PI * 2 * 440 / 8000) * .03, true);
            globalThis.signal = btoa(String.fromCharCode(...bytes));
        })()`, awaitPromise: true, userGesture: false });
        const deniedCall = await cdp.send('Runtime.evaluate', { expression: "output.play('blocked', signal, 8000, 1, false)", awaitPromise: true, returnByValue: true, userGesture: false });
        const denied = deniedCall.result.value;
        if (denied !== 'unavailable') throw Error('Fresh page autoplay was not denied: ' + denied);
        checks.push('actual-autoplay-denial-resolves-without-hanging');
        await page.getByRole('button').click();
        for (let i = 0; i < 10; i++) {
            const completed = await page.evaluate(i => output.play('sound-' + i, signal, 8000, 1, false), i);
            if (completed !== 'completed') throw Error('Playback did not finish');
        }
        checks.push('ten-real-context-completions-and-replacements');
        await page.evaluate(() => { globalThis.result = null; void output.play('loop', signal, 8000, 1, true).then(value => globalThis.result = value); });
        await page.waitForTimeout(350);
        if (await page.evaluate(() => result) !== null) throw Error('Loop completed early');
        await page.evaluate(() => output.cancel('loop'));
        await page.waitForFunction(() => result === 'cancelled'); checks.push('loop-remains-pending-until-cancel');
        const later = await page.evaluate(async () => { const pending = output.play('new', signal, 8000, 1, false); output.cancel('loop'); return pending; });
        if (later !== 'completed') throw Error('Stale cancel affected replacement'); checks.push('late-cancel-cannot-stop-replacement');
        const malformed = await page.evaluate(() => output.play('bad', 'AA==', 8000, 1, false));
        if (malformed !== 'unavailable') throw Error('Malformed samples accepted'); checks.push('malformed-pcm-rejected');
        await page.evaluate(() => { globalThis.result = null; void output.play('unload', signal, 8000, 1, true).then(value => globalThis.result = value); dispatchEvent(new PageTransitionEvent('pagehide')); });
        await page.waitForFunction(() => result === 'cancelled'); checks.push('pagehide-releases-active-context');
        fs.writeFileSync(report, JSON.stringify({ browser: browser.version(), checks,
            audioSha256: crypto.createHash('sha256').update(source).digest('hex'),
            driverSha256: crypto.createHash('sha256').update(fs.readFileSync(__filename)).digest('hex') }, null, 2));
        console.log('Passed ' + checks.length + ' browser audio service checks');
    } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
