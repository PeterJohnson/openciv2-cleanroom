export module openciv.network.sections;

import std;
export import openciv.formats.bytes;
export import openciv.world.geometry;

export namespace openciv::network {
/// N02 §5 and Appendix C. Wire checksum deliberately ignores part of an even
/// extent not divisible by four. Negative sizes perform no reads and sum zero.
std::uint32_t section_checksum(std::span<const formats::Byte> backing, std::int32_t size);
/// Wire metadata describing one materialized state section.
struct SectionDescriptor {
    std::int32_t size = 0, padded_size = 0, id = 0;
    std::uint32_t checksum = 0;
    std::int32_t mirror_offset = 0;
    bool operator==(const SectionDescriptor&) const = default;
};
inline constexpr std::size_t section_count = 24, section_prefix_size = 20;
/// Serialize a section descriptor as five explicit little-endian words.
formats::Bytes encode_section_descriptor(const SectionDescriptor& descriptor);
/// Decode a section descriptor from its fixed wire prefix.
SectionDescriptor decode_section_descriptor(std::span<const formats::Byte> bytes);
/// One raw-image block containing section metadata and owned payload bytes.
struct ImageBlock {
    SectionDescriptor descriptor;
    formats::Bytes data;
    bool operator==(const ImageBlock&) const = default;
};
/// Pure raw-image codecs; they never apply an image to a world or restore options.
formats::Bytes encode_raw_image(std::span<const ImageBlock> blocks);
/// Decode raw image blocks without applying them to live state.
std::vector<ImageBlock> decode_raw_image(std::span<const formats::Byte> bytes);
/// Compute the classic section extents for the supplied map geometry.
std::array<std::int32_t, section_count> classic_section_sizes(const world::Geometry& geometry);
/// A materialized wire section with explicit backing for its declared padded extent.
struct StateSection {
    SectionDescriptor descriptor;
    formats::Bytes backing; // Includes established padding through padded_size.
};
using StateSections = std::array<StateSection, section_count>;
/// Completion or failure reason when applying a raw image sequentially.
enum class ImageApplyStop {
    complete,
    truncated,
    invalid_size,
    invalid_section,
    destination_extent
};
/// Image-application outcome with committed block and consumed-byte counts.
struct ImageApplyResult {
    ImageApplyStop stop = ImageApplyStop::complete;
    std::size_t applied = 0, consumed = 0;
};
/// N02 section 6 block-copy primitive over materialized wire sections. Applies
/// sequentially, keeping prior blocks on rejection; it does not prevalidate the
/// whole image. Ignores received checksum/padding/mirror-offset for destination
/// selection and computes each live checksum from incoming data after the copy.
/// Input is disjoint from section backing. World publication, option restoration,
/// registration, map release/allocation and event relocation belong to the caller.
ImageApplyResult apply_raw_image(StateSections& sections, std::span<const formats::Byte> bytes);
/// N02 §5 registration primitive. Event-pool lock and wire materialization are
/// caller preconditions; no source-world native object bytes are read here.
void register_sections(StateSections& sections);
/// Owned comparison bytes and retained scan positions for state synchronization.
struct StateMirror {
    formats::Bytes bytes;
    std::size_t scan = 0, scan_end = 0, section = 0, section_offset = 0;
};
/// Allocate comparison backing and initialize scan state for the local host/client role.
void allocate_mirror(StateMirror& mirror, const StateSections& sections, bool host);
/// Invalidate comparison bytes so the registered sections will be reconsidered.
void invalidate_mirror(StateMirror& mirror, const StateSections& sections);
/// Select the section subset and extent rules used to build a raw image.
enum class ImageKind { full, delta, join, map_header };
/// N02 §6 prefix through raw-image construction, BEFORE the compression/allocation
/// tail. Updates live checksums in specified order; full/delta transmit old values.
formats::Bytes build_raw_image(StateSections& sections, ImageKind kind, std::size_t unit_scan,
                               std::size_t city_scan);
} // namespace openciv::network
