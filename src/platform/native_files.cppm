export module openciv.platform.native_files;

import std;
export import openciv.formats.bytes;
export import openciv.base.cancellation;

export extern "C++" {
    namespace openciv::io {
    // Native client adapter. Portable formats/queries never open a path.
    formats::Bytes read_file(const std::filesystem::path& path);
    // Publish a new complete file without overwriting any existing destination.
    // Stage in the destination directory, then atomically publish. Errors/cancellation
    // remove the staging file; callbacks run only before publication and may throw.
    void write_new_file(const std::filesystem::path& path, std::span<const formats::Byte> bytes,
                        base::CancellationToken stop = {},
                        const std::function<void(std::size_t)>& progress = {});
    } // namespace openciv::io
}
