// Independent nearest-sampling diagnostic: no Skia, Avalonia, map code or assets.
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { chromium } = require('playwright');

async function main() {
    if (require('playwright/package.json').version !== require('../../dependencies.json').tools.playwright)
        throw Error('Unpinned Playwright');
    const [output] = process.argv.slice(2);
    if (!output) throw Error('Usage: probe_webgl_sampling.cjs OUTPUT.json');
    const results = [];
    for (const hardware of [false, true]) {
        const browser = await chromium.launch({ headless: true, executablePath: process.env.OC2_CHROMIUM_EXECUTABLE,
            args: hardware ? ['--enable-gpu', '--ignore-gpu-blocklist', '--disable-software-rasterizer', '--use-angle=gl']
                : ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
        try {
            const page = await browser.newPage();
            const result = await page.evaluate(() => {
                const canvas = document.createElement('canvas');
                const width = canvas.width = 720, height = canvas.height = 560, side = 260;
                const gl = canvas.getContext('webgl2', { alpha: false, antialias: false, premultipliedAlpha: false });
                if (!gl) throw Error('WebGL2 unavailable');
                const extension = gl.getExtension('WEBGL_debug_renderer_info');
                const renderer = gl.getParameter(extension ? extension.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
                gl.disable(gl.DITHER);
                const pixels = new Uint8Array(side * side * 4);
                for (let y = 0; y < side; y++) for (let x = 0; x < side; x++) {
                    const i = (y * side + x) * 4;
                    pixels[i] = x & 255; pixels[i + 1] = y & 255;
                    pixels[i + 2] = (x >> 8) * 64 + (y >> 8) * 16; pixels[i + 3] = 255;
                }
                gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, side, side, 0, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
                for (const parameter of [gl.TEXTURE_MIN_FILTER, gl.TEXTURE_MAG_FILTER])
                    gl.texParameteri(gl.TEXTURE_2D, parameter, gl.NEAREST);
                for (const parameter of [gl.TEXTURE_WRAP_S, gl.TEXTURE_WRAP_T])
                    gl.texParameteri(gl.TEXTURE_2D, parameter, gl.CLAMP_TO_EDGE);
                const compile = (kind, source) => {
                    const shader = gl.createShader(kind);
                    gl.shaderSource(shader, source); gl.compileShader(shader);
                    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
                    return shader;
                };
                const rows = [];
                for (const scale of [Math.fround(1.2499), Math.fround(1.2501), Math.fround(2.4999)]) {
                    for (const mode of ['varying-nearest', 'varying-centers', 'fragment-centers', 'aligned-varying-centers']) {
                        const target = [23, 17, Math.fround(23 + Math.fround(256 * scale)), Math.fround(17 + Math.fround(256 * scale))];
                        const shape = mode.startsWith('aligned') ? target.map(x => Math.floor(x + .5)) : target;
                        const vertex = `#version 300 es
                            precision highp float;
                            in vec2 point;
                            out highp vec2 coord;
                            void main() {
                                coord = point;
                                gl_Position = vec4(point / vec2(720, 560) * 2.0 - 1.0, 0, 1);
                            }`;
                        const expression = mode === 'varying-nearest' ? 'texture(image, source / 260.0)'
                            : mode === 'fragment-centers' ? 'texelFetch(image, ivec2(floor(source)), 0)'
                                : 'texture(image, (floor(source) + vec2(0.5)) / 260.0)';
                        const fragment = `#version 300 es
                            precision highp float;
                            in highp vec2 coord;
                            uniform sampler2D image;
                            uniform vec4 mapping;
                            out vec4 color;
                            void main() {
                                vec2 source = ${mode === 'fragment-centers' ? 'gl_FragCoord.xy' : 'coord'} * mapping.xy + mapping.zw;
                                color = ${expression};
                            }`;
                        const program = gl.createProgram();
                        gl.attachShader(program, compile(gl.VERTEX_SHADER, vertex));
                        gl.attachShader(program, compile(gl.FRAGMENT_SHADER, fragment));
                        gl.linkProgram(program);
                        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
                        gl.useProgram(program);
                        const sx = Math.fround(256 / Math.fround(target[2] - target[0]));
                        const sy = Math.fround(256 / Math.fround(target[3] - target[1]));
                        gl.uniform4f(gl.getUniformLocation(program, 'mapping'), sx, sy,
                            Math.fround(2 - Math.fround(23 * sx)), Math.fround(2 - Math.fround(17 * sy)));
                        const [left, bottom, right, top] = shape;
                        gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
                        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
                            left, bottom, right, bottom, left, top, left, top, right, bottom, right, top
                        ]), gl.STATIC_DRAW);
                        const attribute = gl.getAttribLocation(program, 'point');
                        gl.enableVertexAttribArray(attribute); gl.vertexAttribPointer(attribute, 2, gl.FLOAT, false, 0, 0);
                        gl.clearColor(0, 0, 0, 1); gl.clear(gl.COLOR_BUFFER_BIT); gl.drawArrays(gl.TRIANGLES, 0, 6);
                        const read = new Uint8Array(width * height * 4);
                        gl.readPixels(0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, read);
                        let different = 0;
                        const first = [];
                        // Compare source identities in covered pixels. Framebuffer and
                        // source rows both use bottom-left origins in this standalone probe.
                        for (let y = 17; y < Math.min(height, Math.floor(target[3] + .5)); y++) {
                            for (let x = 23; x < Math.min(width, Math.floor(target[2] + .5)); x++) {
                                const tx = Math.floor(2 + (x + .5 - 23) * 256 / (target[2] - 23));
                                const ty = Math.floor(2 + (y + .5 - 17) * 256 / (target[3] - 17));
                                const actual = (y * width + x) * 4, expected = (ty * side + tx) * 4;
                                if (read[actual] !== pixels[expected] || read[actual + 1] !== pixels[expected + 1] || read[actual + 2] !== pixels[expected + 2]) {
                                    different++;
                                    if (first.length < 4) first.push({ x, y, expected: [tx, ty], actual: Array.from(read.subarray(actual, actual + 3)) });
                                }
                            }
                        }
                        rows.push({ scale, mode, different, first, error: gl.getError() });
                    }
                }
                return { renderer, rows };
            });
            if (hardware && /SwiftShader|llvmpipe/i.test(result.renderer)) throw Error('Hardware WebGL required');
            results.push({ hardware, browser: browser.version(), ...result });
        } finally { await browser.close(); }
    }
    fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
    fs.writeFileSync(output, JSON.stringify({
        format: 'openciv2-webgl-sampling-diagnostic/1',
        driverSha256: crypto.createHash('sha256').update(fs.readFileSync(__filename)).digest('hex'), results
    }, null, 2) + '\n');
    for (const group of results) for (const row of group.rows)
        console.log(group.hardware ? 'hardware' : 'SwiftShader', row.scale, row.mode, row.different, row.error);
    if (results.some(group => group.rows.some(row => row.error !== 0 ||
        ['fragment-centers', 'aligned-varying-centers'].includes(row.mode) && row.different !== 0)))
        throw Error('WebGL operation or explicit-center regression failed; diagnostic retained');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
