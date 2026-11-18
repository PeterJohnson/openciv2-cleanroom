// Inject before application scripts. Standard WebAssembly API observation only;
// no private .NET heap pointers, forced collection or gameplay access.
module.exports = function observeWasmMemory() {
    const memories = [], known = new WeakSet(), Memory = WebAssembly.Memory;
    const retain = memory => { if (memory instanceof Memory && !known.has(memory)) { known.add(memory); memories.push(memory); } };
    const instanceMemory = result => {
        const instance = result instanceof WebAssembly.Instance ? result : result.instance;
        for (const value of Object.values(instance.exports)) retain(value);
        return result;
    };
    WebAssembly.Memory = new Proxy(WebAssembly.Memory, {
        construct(target, args) { const memory = Reflect.construct(target, args); retain(memory); return memory; }
    });
    // A module may define/export memory without invoking the JS constructor.
    for (const name of ['instantiate', 'instantiateStreaming']) {
        const original = WebAssembly[name];
        WebAssembly[name] = async (...args) => instanceMemory(await original(...args));
    }
    WebAssembly.Instance = new Proxy(WebAssembly.Instance, {
        construct(target, args) { return instanceMemory(Reflect.construct(target, args)); }
    });
    // WASM memory cannot shrink. Strong references retain every observed memory
    // for the navigation; sum is a conservative bound, not process RSS.
    globalThis.oc2WasmCapacity = () => ({ memories: memories.length, bytes: memories.reduce((n, m) => n + m.buffer.byteLength, 0) });
};
