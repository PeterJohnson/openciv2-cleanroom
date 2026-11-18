try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    globalThis.conformanceAdapter = exports.OpenCiv2.ConformanceBrowser.BrowserAdapter;
    document.getElementById('status').textContent = 'Conformance adapter ready';
} catch (error) {
    globalThis.conformanceFailure = String(error);
    document.getElementById('status').textContent = String(error);
}
