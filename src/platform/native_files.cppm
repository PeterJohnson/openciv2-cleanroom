export module openciv.platform.native_files;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.bytes;
export import openciv.base.cancellation;

export namespace openciv::io {
/// Native client adapter. Portable formats/queries never open a path.
formats::Bytes read_file(const std::filesystem::path& path);
/// Publish a new complete file without overwriting any existing destination.
/// Stage in the destination directory, then atomically publish. Errors/cancellation
/// remove the staging file; callbacks run only before publication and may throw.
void write_new_file(const std::filesystem::path& path, std::span<const formats::Byte> bytes,
                    base::CancellationToken stop = {},
                    const base::CallbackRef<void(std::size_t)>& progress = {});
} // namespace openciv::io

// Implementation in the primary unit; platform companions share this module.
namespace openciv::io {
/// Read a native file into owned bytes, reporting open and read failures.
formats::Bytes read_file(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input)
        throw std::runtime_error("Cannot open input file");
    formats::Bytes bytes{std::istreambuf_iterator<char>{input}, {}};
    if (input.bad())
        throw std::runtime_error("Input file read failed");
    return bytes;
}
} // namespace openciv::io
