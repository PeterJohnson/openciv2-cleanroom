import std;
import openciv.network.sections;

namespace openciv::network {
namespace {
using namespace formats;
void admitted(const StateSection& section) {
    const auto& d = section.descriptor;
    if (d.size < 0 || d.padded_size < d.size || d.padded_size % 4 != 0 ||
        static_cast<std::size_t>(d.padded_size) > section.backing.size())
        throw std::invalid_argument("State section lacks declared data/padding backing");
}
void copy_to_mirror(StateMirror& mirror, const StateSections& sections) {
    std::size_t position = 0;
    for (std::size_t i = 0; i < 23; ++i) {
        const auto& section = sections[i];
        admitted(section);
        const auto padded = static_cast<std::size_t>(section.descriptor.padded_size);
        if (position > mirror.bytes.size() || padded > mirror.bytes.size() - position)
            throw std::invalid_argument("Section does not fit mirror backing");
        std::copy_n(section.backing.begin(), section.descriptor.size,
                    mirror.bytes.data() + position);
        position += padded;
    }
}
} // namespace
std::uint32_t section_checksum(std::span<const formats::Byte> backing, std::int32_t size) {
    if (size <= 0)
        return 0;
    ByteReader reader{backing};
    std::uint32_t total = 0;
    if (size % 4 == 0) {
        for (std::int32_t i = 0; i < size / 4; ++i)
            total += reader.u32();
    } else if (size % 2 == 0) {
        for (std::int32_t i = 0; i < size / 4; ++i)
            total += reader.u16();
    } else {
        for (std::int32_t i = 0; i < size; ++i)
            total += reader.u8();
    }
    return total;
}
formats::Bytes encode_section_descriptor(const SectionDescriptor& descriptor) {
    ByteWriter w;
    w.i32(descriptor.size);
    w.i32(descriptor.padded_size);
    w.i32(descriptor.id);
    w.u32(descriptor.checksum);
    w.i32(descriptor.mirror_offset);
    return std::move(w.bytes);
}
SectionDescriptor decode_section_descriptor(std::span<const formats::Byte> bytes) {
    if (bytes.size() != 20)
        throw DecodeError(std::min<std::size_t>(bytes.size(), 20),
                          "Incorrect section prefix extent");
    ByteReader r{bytes};
    return {r.i32(), r.i32(), r.i32(), r.u32(), r.i32()};
}
formats::Bytes encode_raw_image(std::span<const ImageBlock> blocks) {
    ByteWriter w;
    for (const auto& block : blocks) {
        if (block.descriptor.size < 0 ||
            block.data.size() != static_cast<std::size_t>(block.descriptor.size) ||
            block.descriptor.id < 0 || block.descriptor.id >= 24)
            throw std::invalid_argument("Image block outside admitted ID/size domain");
        w.append(encode_section_descriptor(block.descriptor));
        w.append(block.data);
    }
    return std::move(w.bytes);
}
std::vector<ImageBlock> decode_raw_image(std::span<const formats::Byte> bytes) {
    ByteReader reader{bytes};
    std::vector<ImageBlock> blocks;
    while (!reader.remaining().empty()) {
        const auto offset = reader.position();
        const auto descriptor = decode_section_descriptor(reader.take(20));
        if (descriptor.size < 0 || descriptor.id < 0 || descriptor.id >= 24)
            throw DecodeError(offset, "Image block outside admitted ID/size domain");
        const auto data = reader.take(static_cast<std::size_t>(descriptor.size));
        blocks.push_back({descriptor, {data.begin(), data.end()}});
    }
    return blocks;
}
ImageApplyResult apply_raw_image(StateSections& sections, std::span<const formats::Byte> bytes) {
    ImageApplyResult result;
    const auto stop = [&](ImageApplyStop reason) {
        result.stop = reason;
        return result;
    };
    while (result.consumed < bytes.size()) {
        if (bytes.size() - result.consumed < section_prefix_size)
            return stop(ImageApplyStop::truncated);
        const auto descriptor =
            decode_section_descriptor(bytes.subspan(result.consumed, section_prefix_size));
        result.consumed += section_prefix_size;
        if (descriptor.size < 0)
            return stop(ImageApplyStop::invalid_size);
        if (descriptor.id < 0 || descriptor.id >= static_cast<std::int32_t>(section_count))
            return stop(ImageApplyStop::invalid_section);
        const auto size = static_cast<std::size_t>(descriptor.size);
        if (size > bytes.size() - result.consumed)
            return stop(ImageApplyStop::truncated);
        auto& section = sections[static_cast<std::size_t>(descriptor.id)];
        if (size > section.backing.size())
            return stop(ImageApplyStop::destination_extent);
        const auto incoming = bytes.subspan(result.consumed, size);
        std::copy(incoming.begin(), incoming.end(), section.backing.begin());
        section.descriptor.checksum = section_checksum(incoming, descriptor.size);
        result.consumed += size;
        ++result.applied;
    }
    return result;
}
std::array<std::int32_t, section_count> classic_section_sizes(const world::Geometry& geometry) {
    const auto coarse = geometry.coarse_width() * geometry.coarse_height();
    const auto area = static_cast<std::int32_t>(geometry.area());
    return {4,     0x14c, 0x790, 0x2ca0, 0x400, 0x10000, 0x5800,   0x3f0,
            0x4b0, 100,   0x154, 0x494,  16,    coarse,  coarse,   area,
            area,  area,  area,  area,   area,  area,    area * 6, 50000};
}
void register_sections(StateSections& sections) {
    std::int32_t offset = 0;
    for (std::size_t i = 0; i < sections.size(); ++i) {
        auto& section = sections[i];
        auto& d = section.descriptor;
        if (d.size < 0 || d.size > std::numeric_limits<std::int32_t>::max() - 3)
            throw std::invalid_argument("Section size outside nonwrapping backing domain");
        d.id = static_cast<std::int32_t>(i);
        d.padded_size = (d.size + 3) & ~3;
        d.checksum = 0;
        admitted(section);
    }
    // Registration writes all descriptors before assigning any mirrored offsets.
    for (std::size_t i = 0; i < 23; ++i) {
        auto& d = sections[i].descriptor;
        d.mirror_offset = offset;
        if (d.padded_size > std::numeric_limits<std::int32_t>::max() - offset)
            throw std::invalid_argument("Mirror extent exceeds nonwrapping backing domain");
        offset += d.padded_size;
    }
}
void allocate_mirror(StateMirror& mirror, const StateSections& sections, bool host) {
    Bytes{}.swap(mirror.bytes); // Old mirror is gone before allocation or later failure.
    std::size_t size = 0;
    for (std::size_t i = 0; i < 23; ++i) {
        admitted(sections[i]);
        const auto padded = static_cast<std::size_t>(sections[i].descriptor.padded_size);
        if (padded > std::numeric_limits<std::size_t>::max() - size)
            throw std::length_error("Mirror size exceeds host backing domain");
        size += padded;
    }
    mirror.bytes.assign(size, 0);
    mirror.scan_end = host ? size : static_cast<std::size_t>(sections[15].descriptor.mirror_offset);
    if (mirror.scan_end > size)
        throw std::invalid_argument("Mirror scan end outside admitted backing");
    copy_to_mirror(mirror, sections);
    mirror.scan = mirror.section = mirror.section_offset = 0;
}
void invalidate_mirror(StateMirror& mirror, const StateSections& sections) {
    copy_to_mirror(mirror, sections);
    for (auto& byte : mirror.bytes)
        byte = static_cast<Byte>(~byte);
    mirror.scan = mirror.section = mirror.section_offset = 0;
}
formats::Bytes build_raw_image(StateSections& sections, ImageKind kind, std::size_t unit_scan,
                               std::size_t city_scan) {
    std::vector<std::size_t> selected;
    switch (kind) {
    case ImageKind::join:
        selected = {1, 7, 3, 2, 9, 11, 23};
        break;
    case ImageKind::map_header:
        selected = {1, 12};
        break;
    case ImageKind::full:
    case ImageKind::delta:
        for (std::size_t i = 0; i < 24; ++i)
            selected.push_back(i);
        break;
    default:
        throw std::invalid_argument("Unknown image kind");
    }
    ByteWriter writer;
    for (auto index : selected) {
        auto& section = sections[index];
        admitted(section);
        auto prefix = section.descriptor;
        const bool old_checksum = kind == ImageKind::full || kind == ImageKind::delta;
        if (old_checksum && (index == 5 || index == 6)) {
            const auto scan = index == 5 ? unit_scan : city_scan;
            const auto extent = index == 5 ? 32U : 88U;
            if (scan > static_cast<std::size_t>(prefix.size) / extent)
                throw std::invalid_argument("Image scan bound exceeds registered section");
            prefix.size = static_cast<std::int32_t>(scan * extent);
        }
        const auto checksum = section_checksum(section.backing, prefix.size);
        if (kind == ImageKind::delta &&
            (prefix.size == 0 || checksum == section.descriptor.checksum))
            continue;
        section.descriptor.checksum = checksum;
        if (!old_checksum)
            prefix.checksum = checksum;
        writer.append(encode_section_descriptor(prefix));
        writer.append(std::span{section.backing}.first(static_cast<std::size_t>(prefix.size)));
    }
    return std::move(writer.bytes);
}
} // namespace openciv::network
