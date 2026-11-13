// Run after building WASM: node tests/web_smoke.cjs build/web
// Requires Playwright and its Chromium browser. The local server is temporary.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { createServer } = require('node:http');
const { readFile, mkdir } = require('node:fs/promises');
const { resolve, basename, extname } = require('node:path');
const assert = require('node:assert/strict');

(async () => {
  const root = resolve(process.argv[2] || 'build/web');
  const captures = resolve(root, 'screenshots');
  await mkdir(captures, { recursive: true });
  const server = createServer(async (request, response) => {
    const name = basename(new URL(request.url, 'http://localhost').pathname);
    if (!['openciv2.html', 'openciv2.js', 'openciv2.wasm'].includes(name)) {
      response.writeHead(404).end();
      return;
    }
    try {
      const data = await readFile(resolve(root, name));
      const mime = { '.html': 'text/html', '.js': 'text/javascript', '.wasm': 'application/wasm' };
      response.writeHead(200, { 'Content-Type': mime[extname(name)] }).end(data);
    } catch { response.writeHead(404).end(); }
  });
  // A failed bind must reject the test, not leave its top-level promise pending.
  await new Promise((done, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', done);
  });
  let browser;
  let page;
  try {
    browser = await chromium.launch({
      executablePath: process.env.CHROMIUM_EXECUTABLE || undefined,
      args: ['--enable-unsafe-swiftshader']
    });
    page = await browser.newPage({ viewport: { width: 1000, height: 820 } });
    page.setDefaultTimeout(15000);
    const errors = [];
    page.on('pageerror', error => { errors.push(String(error)); console.error('Browser:', error); });
    page.on('console', message => { if (message.type() === 'error') console.error('Console:', message.text()); });
    await page.goto(`http://127.0.0.1:${server.address().port}/openciv2.html`);
    await page.waitForFunction(() => document.getElementById('status').textContent === 'Ready');
    let canvas = page.locator('#canvas');
    // Let input and presentation pass through multiple browser/SDL callbacks.
    const settle = () => page.evaluate(() => new Promise(done => {
      let frames = 12;
      function tick() { if (--frames === 0) done(); else requestAnimationFrame(tick); }
      requestAnimationFrame(tick);
    }));
    const click = async (x, y) => {
      const bounds = await canvas.boundingBox();
      await page.mouse.click(bounds.x + x, bounds.y + y);
      await settle();
    };
    // ImGui lays out in window points, not the world's 960x640 logical space.
    const titleButton = async index => {
      const bounds = await canvas.boundingBox();
      await click(bounds.width / 2, bounds.height / 2 - 29 + index * 52);
    };
    const region = async (x, y, width, height) => {
      const bounds = await canvas.boundingBox();
      return page.screenshot({ clip: {
        x: bounds.x + x * bounds.width / 960, y: bounds.y + y * bounds.height / 640,
        width: width * bounds.width / 960, height: height * bounds.height / 640
      } });
    };
    const key = async value => { await page.keyboard.press(value); await settle(); };
    await settle();
    await canvas.screenshot({ path: resolve(captures, 'title.png') });
    await titleButton(0);
    await canvas.screenshot({ path: resolve(captures, 'dialog.png') });
    const blockedMenu = await region(0, 0, 960, 150);
    await click(40, 16); // The main menu is behind the modal.
    // Ignore the keyboard focus outline, which hides on pointer input.
    await page.mouse.move(0, 0);
    await settle();
    assert.ok(blockedMenu.equals(await region(0, 0, 960, 150)), 'modal must block menu clicks');
    await key('Escape');
    await titleButton(0);
    await key('Enter');
    await canvas.screenshot({ path: resolve(captures, 'session.png') });
    const grid = await region(48, 300, 864, 220);
    await key('F10');
    await key('ArrowRight');
    await key('ArrowDown');
    await key('Enter');
    assert.ok(!grid.equals(await region(48, 300, 864, 220)), 'menu must toggle the visible grid');
    await click(130, 140); // Return to Title.

    // Verify pointer coordinate conversion with a CSS-scaled browser canvas.
    await page.setViewportSize({ width: 620, height: 700 });
    await settle();
    await titleButton(0);
    await key('Escape');
    await titleButton(2); // Quit button.
    await key('Enter'); // Default Cancel must keep the application running.
    assert.equal(await page.locator('#status').textContent(), 'Ready');
    await titleButton(2);
    await key('ArrowLeft');
    await key('Enter');
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('Session ended'));
    assert.deepEqual(errors, [], 'browser runtime errors');
    await page.getByRole('button', { name: 'Restart', exact: true }).click();
    await page.waitForFunction(() => document.getElementById('status').textContent === 'Ready');
    assert.deepEqual(errors, [], 'restart runtime errors');

    // A dense framebuffer must preserve window-point UI size and hit targets.
    await page.close();
    page = await browser.newPage({ viewport: { width: 1000, height: 820 }, deviceScaleFactor: 2 });
    page.setDefaultTimeout(15000);
    page.on('pageerror', error => errors.push(String(error)));
    canvas = page.locator('#canvas');
    await page.goto(`http://127.0.0.1:${server.address().port}/openciv2.html`);
    await page.waitForFunction(() => document.getElementById('status').textContent === 'Ready');
    await settle();
    const density = await canvas.evaluate(element => element.width / element.getBoundingClientRect().width);
    assert.ok(Math.abs(density - 2) < 0.02, 'HiDPI canvas must use a dense framebuffer');
    const title = await region(48, 300, 864, 220);
    await titleButton(0);
    await canvas.screenshot({ path: resolve(captures, 'hidpi-dialog.png') });
    await key('Enter');
    assert.ok(!title.equals(await region(48, 300, 864, 220)), 'HiDPI pointer/default-button activation must open the game view');
    await click(130, 140);
    await titleButton(2);
    await key('ArrowLeft');
    await key('Enter');
    await page.waitForFunction(() => document.getElementById('status').textContent.includes('Session ended'));
    assert.deepEqual(errors, [], 'HiDPI runtime errors');
    console.log('WASM startup, dialogs, menu input, resizing, HiDPI, quit and restart passed');
  } catch (error) {
    if (page) {
      console.error('Page status:', await page.locator('#status').textContent().catch(() => 'unavailable'));
      await page.screenshot({ path: resolve(captures, 'failure.png') }).catch(() => {});
    }
    throw error;
  } finally {
    if (browser) await browser.close();
    await new Promise(done => server.close(done));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
