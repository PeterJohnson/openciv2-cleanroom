// The host cannot reuse a lost graphics context. Keep recovery outside the
// canvas so it remains visible and operable even when rendering has stopped.
export function showFailure(error) {
    globalThis.openCiv2Failure = String(error);
    document.getElementById('loading').hidden = true;
    document.getElementById('error-message').textContent = String(error);
    document.getElementById('error').hidden = false;
}

document.getElementById('reload').addEventListener('click', () => location.reload());
document.getElementById('out').addEventListener('webglcontextlost', () => {
    showFailure('The graphics connection was lost. Reload to reopen this preview.');
}, true);
