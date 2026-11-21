// Production UI playback tests. Observe the actual Web Audio signal and inject device denial at its API.
module.exports = async function ({ page, click, check, read, waitIdle }) {
    const wave = seconds => {
        const rate = 22050, frames = Math.floor(rate * seconds), data = Buffer.alloc(44 + frames * 2);
        data.write('RIFF'); data.writeUInt32LE(data.length - 8, 4); data.write('WAVEfmt ', 8); data.writeUInt32LE(16, 16);
        data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22); data.writeUInt32LE(rate, 24); data.writeUInt32LE(rate * 2, 28);
        data.writeUInt16LE(2, 32); data.writeUInt16LE(16, 34); data.write('data', 36); data.writeUInt32LE(frames * 2, 40);
        for (let i = 0; i < frames; i++) data.writeInt16LE(Math.round(1200 * Math.sin(i * Math.PI * 2 * 440 / rate)), 44 + i * 2);
        return data;
    };
    const importWave = async (name, buffer) => {
        const chosen = page.waitForEvent('filechooser'); await click('import');
        await (await chosen).setFiles({ name, mimeType: 'audio/wav', buffer }); await waitIdle();
    };
    await page.evaluate(() => {
        globalThis.audioStarts = [];
        const start = AudioBufferSourceNode.prototype.start;
        AudioBufferSourceNode.prototype.start = function (...args) {
            globalThis.audioStarts.push({ channels: this.buffer.numberOfChannels, rate: this.buffer.sampleRate,
                frames: this.buffer.length, samples: Array.from(this.buffer.getChannelData(0).slice(0, 16)) });
            return start.apply(this, args);
        };
    });
    await importWave('short.wav', wave(.2)); await click('play');
    await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.playbackResult === 'Completed');
    const starts = await page.evaluate(() => globalThis.audioStarts);
    if (starts.length !== 1 || starts[0].rate !== 22050 || starts[0].frames !== 4410 || starts[0].channels !== 1 ||
        starts[0].samples.some((value, i) => value !== Math.round(1200 * Math.sin(i * Math.PI * 2 * 440 / 22050)) / 32768)) throw Error('Authored audio signal changed');
    await check('wav-output-completes-once', s => !s.files.playing && s.effects === 0);
    await click('remove'); await waitIdle();
    await importWave('long.wav', wave(4)); await click('play');
    await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.playing);
    const before = (await read()).serviceTicks;
    await page.waitForFunction(before => globalThis.openCiv2Snapshot().serviceTicks > before + 4, before);
    await check('audio-services-responsive-and-no-double-play', s => s.files.playing && !s.files.controls.play.enabled && !s.files.controls.close.enabled);
    await page.keyboard.press('Escape'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.playbackResult === 'Cancelled');
    await check('audio-escape-cancels-without-effects', s => !s.files.playing && s.effects === 0);
    await page.evaluate(() => { globalThis.resumeAudio = AudioContext.prototype.resume; AudioContext.prototype.resume = () => Promise.reject(new DOMException('Denied', 'NotAllowedError')); });
    await click('play'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.playbackResult === 'Unavailable');
    await check('audio-denial-visible-and-cleaned', s => !s.files.playing && s.files.message.includes('permission denied') && s.effects === 0);
    await page.evaluate(() => { AudioContext.prototype.resume = globalThis.resumeAudio; });
    await click('remove'); await waitIdle();
    await importWave('bad.wav', Buffer.from('RIFF truncated'));
    await click('play'); await page.waitForFunction(() => globalThis.openCiv2Snapshot().files.playbackResult === 'Unavailable');
    await check('malformed-wav-rejected', s => s.files.message.startsWith('Cannot play this file:') && s.effects === 0);
    await click('remove'); await waitIdle();
};
