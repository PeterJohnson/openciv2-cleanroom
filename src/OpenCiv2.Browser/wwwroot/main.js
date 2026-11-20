import { dotnet } from './_framework/dotnet.js';
import { showFailure } from './browser-lifecycle.js';
try {
    const runtime = await dotnet.create();
    runtime.setModuleImports('browser', {
        baseUrl: () => new URL('./', location.href).href,
        option: name => new URL(location.href).searchParams.get(name)
    });
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    // Read-only observations from the same scoped model displayed by the UI; no engine access.
    globalThis.openCiv2Identity = () => JSON.parse(exports.Program.Identity());
    globalThis.openCiv2Snapshot = () => JSON.parse(exports.Program.Snapshot());
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
    document.getElementById('loading').hidden = true;
} catch (error) {
    showFailure(error);
}
