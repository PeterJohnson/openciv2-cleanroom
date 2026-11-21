// HTTP transport compression only. Decoded files retain their pinned identities.
const fs = require('node:fs'), path = require('node:path'), zlib = require('node:zlib');
const root = path.resolve(process.argv[2], 'content');
let original = 0, compressed = 0;
for (const entry of fs.readdirSync(root, { recursive: true, withFileTypes: true })) {
    if (!entry.isFile() || /\.(br|gz)$/.test(entry.name)) continue;
    const file = path.join(entry.parentPath, entry.name), bytes = fs.readFileSync(file);
    const packed = zlib.brotliCompressSync(bytes, { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 5 } });
    original += bytes.length;
    if (packed.length < bytes.length) {
        if (!zlib.brotliDecompressSync(packed).equals(bytes)) throw Error('Compression changed content: ' + file);
        fs.writeFileSync(file + '.br', packed); compressed += packed.length;
    } else { fs.rmSync(file + '.br', { force: true }); compressed += bytes.length; }
}
console.log(JSON.stringify({ originalBytes: original, encodedBytes: compressed, codec: 'Brotli', quality: 5, node: process.version }));
