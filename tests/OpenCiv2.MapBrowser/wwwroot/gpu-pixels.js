// Same opaque RGB metric as GpuPixelChecks.CompareScalar. Keeping the bulk loop
// in the browser test harness avoids millions of interpreted managed span reads.
export function comparePixels(actual, expected) {
    if (actual.length !== expected.length || !actual.length || actual.length % 4) throw Error('Matching RGBA pixels required');
    let different = 0, over8 = 0, maximum = 0, total = 0;
    for (let i = 0; i < actual.length; i += 4) {
        const r = Math.abs(actual[i] - expected[i]);
        const g = Math.abs(actual[i + 1] - expected[i + 1]);
        const b = Math.abs(actual[i + 2] - expected[i + 2]);
        const high = Math.max(r, g, b);
        if (high) different++;
        if (high > 8) over8++;
        maximum = Math.max(maximum, high);
        total += r + g + b;
    }
    const pixels = actual.length / 4;
    return [pixels, different, over8, maximum, total / (pixels * 3)];
}
