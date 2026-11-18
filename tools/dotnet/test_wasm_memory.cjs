// Authored standard WASM module: one memory and an exported memory.grow function.
const assert = require('node:assert/strict');
require('./observe_wasm_memory.cjs')();
const bytes = new Uint8Array([
    0,97,115,109,1,0,0,0,
    1,6,1,96,1,127,1,127,
    3,2,1,0,
    5,3,1,0,1,
    7,17,2,6,109,101,109,111,114,121,2,0,4,103,114,111,119,0,0,
    10,8,1,6,0,32,0,64,0,11
]);
async function main() {
    const direct = new WebAssembly.Memory({ initial: 1 });
    assert.deepEqual(globalThis.oc2WasmCapacity(), { memories: 1, bytes: 65536 });
    direct.grow(1);
    const compiled = new WebAssembly.Module(bytes);
    const sync = new WebAssembly.Instance(compiled);
    assert.equal(sync.exports.grow(2), 1);
    assert.deepEqual(globalThis.oc2WasmCapacity(), { memories: 2, bytes: 5 * 65536 });
    const asyncBytes = await WebAssembly.instantiate(bytes);
    assert.equal(asyncBytes.instance.exports.grow(1), 1);
    const asyncModule = await WebAssembly.instantiate(compiled);
    assert.equal(asyncModule.exports.grow(3), 1);
    const streamed = await WebAssembly.instantiateStreaming(new Response(bytes, { headers: { 'Content-Type': 'application/wasm' } }));
    assert.equal(streamed.instance.exports.grow(1), 1);
    assert.deepEqual(globalThis.oc2WasmCapacity(), { memories: 5, bytes: 13 * 65536 });
    console.log('Observed constructor, sync/async/streaming exports and in-WASM memory growth without duplicate accounting.');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
