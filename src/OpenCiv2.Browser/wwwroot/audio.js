// P10 output only: owned decoded PCM, no game callbacks or implicit sound substitutions.
const active = new Map();
export function cancel(id) { active.get(id)?.finish('cancelled'); }
export function play(id, encoded, rate, channels, loop) {
    if (active.has(id)) return Promise.resolve('unavailable');
    return new Promise(resolve => {
        let context, source, timer, finished = false;
        const finish = result => {
            if (finished) return;
            finished = true; clearTimeout(timer); active.delete(id);
            if (source) { source.onended = null; try { source.stop(); } catch { } source.disconnect(); }
            if (context) { context.onstatechange = null; void context.close().catch(() => {}); }
            resolve(result);
        };
        active.set(id, { finish });
        try {
            if (!Number.isInteger(rate) || rate < 8000 || rate > 192000 || ![1, 2].includes(channels) || encoded.length > 90 * 1024 * 1024)
                throw new Error('Invalid audio');
            const raw = atob(encoded);
            if (!raw.length || raw.length % (channels * 4) || raw.length > 64 * 1024 * 1024) throw new Error('Invalid PCM length');
            const bytes = Uint8Array.from(raw, value => value.charCodeAt(0));
            const samples = new DataView(bytes.buffer), frames = bytes.length / (channels * 4);
            context = new AudioContext();
            const buffer = context.createBuffer(channels, frames, rate);
            for (let channel = 0; channel < channels; channel++) {
                const output = buffer.getChannelData(channel);
                for (let frame = 0; frame < frames; frame++) {
                    const value = samples.getFloat32((frame * channels + channel) * 4, true);
                    if (!Number.isFinite(value) || value < -1 || value > 1) throw new Error('Invalid PCM');
                    output[frame] = value;
                }
            }
            source = context.createBufferSource(); source.buffer = buffer; source.loop = loop;
            source.connect(context.destination); source.onended = () => finish('completed');
            // A browser may leave resume() pending indefinitely without user activation.
            timer = setTimeout(() => finish('unavailable'), 1500);
            void context.resume().then(() => {
                if (finished) return;
                if (context.state !== 'running') { finish('unavailable'); return; }
                clearTimeout(timer);
                context.onstatechange = () => { if (context.state !== 'running') finish('unavailable'); };
                source.start();
                if (!loop) timer = setTimeout(() => finish('unavailable'), (buffer.duration + 5) * 1000);
            }).catch(() => finish('unavailable'));
        } catch { finish('unavailable'); }
    });
}
addEventListener('pagehide', () => { for (const playback of [...active.values()]) playback.finish('cancelled'); });
