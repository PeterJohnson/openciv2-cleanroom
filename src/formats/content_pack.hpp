#pragma once
#include "gameplay.hpp"
#include <map>
#include <optional>
#include <vector>

namespace openciv::formats::content {
inline constexpr std::string_view pack_resolver_version = "openciv2-content-resolution/1";
inline constexpr std::array<std::string_view, 4> plain_components{"rules", "cityNames",
                                                                  "cityPreferences", "events"};
inline constexpr std::array<std::string_view, 5> localized_components{
    "text", "encyclopedia", "menus", "documents", "council"};
struct PackIdentity {
    std::string id, revision, sha256;
    bool operator==(const PackIdentity&) const = default;
};
struct Provenance {
    std::vector<std::string> authors;
    std::string license;
    std::vector<std::string> notes;
};
struct PackReceipt {
    PackIdentity pack;
    std::string source_identity;
    Provenance provenance;
};
struct MountedPack {
    Source manifest;
    // Empty directory selects the pack root. A nested gameplay bundle requests
    // a narrower directory; adapters must enforce containment in both mounts.
    std::function<Source(std::string_view directory, std::string_view path)> files;
};
struct ResolvedFile {
    PackIdentity owner;
    FileReceipt receipt;
    Bytes bytes;
};
struct ResolvedDocument {
    std::shared_ptr<const ResolvedFile> file;
    JsonDocument json;
};
enum class ResourceKind { audio, image, video };
struct ResolvedResource {
    std::string id;
    ResourceKind kind;
    std::shared_ptr<const ResolvedFile> file;
};
struct ResolvedScenario {
    std::string id;
    std::shared_ptr<const ResolvedFile> world; // Null means explicitly external.
};
struct ResolvedGameplay {
    PackIdentity owner;
    std::shared_ptr<const ResolvedFile> manifest; // Null for the registered builtin.
    std::shared_ptr<const GameplayBundle> bundle;
};
struct ResolvedPack {
    std::vector<PackReceipt> manifests; // Parent first; selected overlay last.
    std::string profile, default_locale, selected_locale;
    std::map<std::string, ResolvedDocument> components;
    std::map<std::string, std::map<std::string, ResolvedDocument>> localized;
    std::optional<ResolvedDocument> compatibility;
    std::vector<ResolvedScenario> scenarios;
    std::vector<ResolvedResource> resources;
    ResolvedGameplay gameplay;
    // Resolution checks manifest structure, selection, hashes, format/locale
    // envelopes and gameplay operands. Text/rule/event body semantics and world/
    // media decoding still belong to their decoders; this is not session admission.
};
struct ResolutionLimits {
    std::size_t maximum_chain = 64;
    std::size_t maximum_files = 4096;
    std::size_t maximum_bytes = 256 * 1024 * 1024;
};
// Parents are explicit mounts addressed by exact manifest hash. Unselected
// parent manifests and replaced component files are never decoded or opened.
std::shared_ptr<const ResolvedPack>
resolve_pack(const MountedPack& selected, std::span<const MountedPack> registered_parents = {},
             std::string_view requested_locale = {}, bool require_classic = false,
             ResolutionLimits limits = {});
std::shared_ptr<const GameplayBundle> builtin_gameplay();
} // namespace openciv::formats::content
