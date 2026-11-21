// Application storage, never a virtual native path. Values are owned base64 byte images.
const databaseName = 'openciv2-app-v1';
const maximumBytes = 32 * 1024 * 1024;
function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        let rejected = false;
        request.onupgradeneeded = () => request.result.createObjectStore('bytes');
        request.onerror = () => reject(request.error);
        request.onblocked = () => { rejected = true; reject(new Error('Storage upgrade blocked by another tab. Close it and retry.')); };
        request.onsuccess = () => {
            const db = request.result;
            if (rejected) { db.close(); return; }
            db.onversionchange = () => db.close();
            resolve(db);
        };
    });
}
async function transaction(mode, operation) {
    const db = await openDatabase();
    try {
        return await new Promise((resolve, reject) => {
            const tx = db.transaction('bytes', mode, { durability: 'strict' });
            let result;
            tx.oncomplete = () => resolve(result);
            tx.onabort = () => reject(tx.error ?? new Error('Storage transaction aborted'));
            tx.onerror = () => {}; // Abort carries the final failure; request success is not commit.
            try {
                const request = operation(tx.objectStore('bytes'));
                request.onsuccess = () => { result = request.result; };
            } catch (error) { tx.abort(); reject(error); }
        });
    } finally { db.close(); }
}
export async function read(key) { return (await transaction('readonly', store => store.get(key))) ?? null; }
export async function write(key, bytes) { await transaction('readwrite', store => store.put(bytes, key)); }
export async function remove(key) { await transaction('readwrite', store => store.delete(key)); }
export async function list() { return JSON.stringify(await transaction('readonly', store => store.getAllKeys())); }

function encode(bytes) {
    const chunks = [];
    for (let i = 0; i < bytes.length; i += 32768) chunks.push(String.fromCharCode(...bytes.subarray(i, i + 32768)));
    return btoa(chunks.join(''));
}
let cancelSelection;
export function cancelImport() { cancelSelection?.(); }
export function importFile() {
    if (cancelSelection) return Promise.reject(new Error('File selection is already pending'));
    return new Promise((resolve, reject) => {
        const input = document.createElement('input');
        input.type = 'file'; input.hidden = true;
        input.setAttribute('aria-label', 'Import a file');
        let finished = false;
        const finish = (value, error) => {
            if (finished) return;
            finished = true; input.remove(); cancelSelection = undefined;
            if (error) reject(error); else resolve(value);
        };
        cancelSelection = () => finish(null);
        input.oncancel = () => finish(null);
        input.onchange = async () => {
            try {
                const file = input.files?.[0];
                if (!file) { finish(null); return; }
                if (file.size > maximumBytes) throw new Error('Files must be at most 32 MiB');
                const bytes = new Uint8Array(await file.arrayBuffer());
                if (!finished) finish(JSON.stringify({ name: file.name, bytes: encode(bytes) }));
            } catch (error) { finish(null, error); }
        };
        document.body.append(input);
        try { input.click(); } catch (error) { finish(null, error); }
    });
}
export function exportFile(name, bytes) {
    const decoded = atob(bytes), data = new Uint8Array(decoded.length);
    for (let i = 0; i < data.length; i++) data[i] = decoded.charCodeAt(i);
    const url = URL.createObjectURL(new Blob([data], { type: 'application/octet-stream' }));
    const link = document.createElement('a');
    link.href = url; link.download = name; link.hidden = true;
    document.body.append(link);
    try { link.click(); } finally { link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000); }
}
