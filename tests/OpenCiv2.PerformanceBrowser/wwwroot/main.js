try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    globalThis.runWorkloads = (inputs, repetitions) => JSON.parse(exports.OpenCiv2.PerformanceBrowser.BrowserWorkloads.Run(inputs, repetitions));
    globalThis.workloadsReadyMs = performance.now();
    document.getElementById('results').textContent = 'Ready';
} catch (error) {
    globalThis.workloadsError = String(error);
    document.getElementById('results').textContent = String(error);
}
