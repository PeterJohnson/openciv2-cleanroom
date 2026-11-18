try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    const report = JSON.parse(exports.OpenCiv2.AutomationBrowser.BrowserTests.Run());
    globalThis.automationResults = report;
    document.getElementById('results').textContent = JSON.stringify(report, null, 2);
} catch (error) {
    globalThis.automationResults = { Passed: false, Error: String(error) };
    document.getElementById('results').textContent = String(error);
}
