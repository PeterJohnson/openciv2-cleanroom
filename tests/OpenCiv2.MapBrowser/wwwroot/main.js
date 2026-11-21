import { dotnet } from './_framework/dotnet.js';
import { comparePixels } from './gpu-pixels.js';
import * as storage from './storage.js';
import * as audio from './audio.js';
import { showFailure } from './browser-lifecycle.js';
try {
    const parameters = new URL(location.href).searchParams;
    const features = parameters.get('gpuFeatures');
    if (features) {
        // Arbitrary feature selection exists only in this test host.
        dotnet.withEnvironmentVariables({ OPENCIV2_MAP_EXPERIMENT: '1', OPENCIV2_MAP_GPU_FEATURES: features });
    }
    const runtime = await dotnet.create();
    runtime.setModuleImports('storage', storage);
    runtime.setModuleImports('audio', audio);
    runtime.setModuleImports('browser', {
        comparePixels,
        baseUrl: () => new URL('./', location.href).href,
        option: name => new URL(location.href).searchParams.get(name)
    });
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    globalThis.openCiv2Identity = () => JSON.parse(exports.Program.Identity());
    globalThis.openCiv2Snapshot = () => JSON.parse(exports.Program.Snapshot());
    globalThis.openCiv2GpuHistory = async () => JSON.parse(await exports.Program.RunGpuHistory());
    globalThis.openCiv2GpuSampling = async () => JSON.parse(await exports.Program.RunGpuSampling());
    globalThis.openCiv2Matrix = async () => JSON.parse(await exports.Program.RunMatrix());
    globalThis.openCiv2ResultFiles = () => JSON.parse(exports.Program.ResultFiles());
    globalThis.openCiv2ResultFile = path => exports.Program.ResultFile(path);
    globalThis.openCiv2FreshFrame = async () => Uint8Array.from(atob(await exports.Program.FreshFrame()), c => c.charCodeAt(0));
    globalThis.openCiv2PerformanceSetup = () => exports.Program.PerformanceSetup();
    globalThis.openCiv2PerformanceSnapshot = () => JSON.parse(exports.Program.PerformanceSnapshot());
    globalThis.openCiv2FreshRendererFrame = () => exports.Program.FreshRendererFrame();
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
    document.getElementById('loading').hidden = true;
} catch (error) {
    showFailure(error);
}
