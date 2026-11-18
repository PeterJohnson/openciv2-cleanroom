import { dotnet } from './_framework/dotnet.js';
try {
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    // Read-only observations from the same scoped model displayed by the UI; no engine access.
    globalThis.openCiv2Identity = () => JSON.parse(exports.Program.Identity());
    globalThis.openCiv2Snapshot = () => JSON.parse(exports.Program.Snapshot());
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
} catch (error) {
    globalThis.openCiv2Failure = String(error);
    const output = document.getElementById('error'); output.hidden = false; output.textContent = String(error);
}
