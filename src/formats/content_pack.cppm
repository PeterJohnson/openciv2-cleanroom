export module openciv.formats.content_pack;

import std;
export import openciv.formats.gameplay;

export namespace openciv::formats::content {
inline constexpr std::string_view pack_resolver_version = "openciv2-content-resolution/1";
inline constexpr std::array<std::string_view, 4> plain_components{"rules", "cityNames",
                                                                  "cityPreferences", "events"};
inline constexpr std::array<std::string_view, 5> localized_components{
    "text", "encyclopedia", "menus", "documents", "council"};
/// A content pack identity pinned by revision and manifest hash.
struct PackIdentity {
    std::string id, revision, sha256;
    bool operator==(const PackIdentity&) const = default;
};
/// Authorship, license, and notes supplied by a content pack.
struct Provenance {
    std::vector<std::string> authors;
    std::string license;
    std::vector<std::string> notes;
};
/// A resolved manifest identity together with its source and provenance.
struct PackReceipt {
    PackIdentity pack;
    std::string source_identity;
    Provenance provenance;
};
/// An explicit manifest mount with a contained relative-file resolver.
struct MountedPack {
    Source manifest;
    /// Empty directory selects the pack root. A nested gameplay bundle requests
    /// a narrower directory; adapters must enforce containment in both mounts.
    std::function<Source(std::string_view directory, std::string_view path)> files;
};
/// Owned, hash-verified file bytes attributed to their supplying pack.
struct ResolvedFile {
    PackIdentity owner;
    FileReceipt receipt;
    Bytes bytes;
};
/// A parsed component document retaining its verified source file.
struct ResolvedDocument {
    std::shared_ptr<const ResolvedFile> file;
    JsonDocument json;
};
/// Kinds of media resource declared by a content pack.
enum class ResourceKind { audio, image, video };
/// A named media resource with its kind and verified backing file.
struct ResolvedResource {
    std::string id;
    ResourceKind kind;
    std::shared_ptr<const ResolvedFile> file;
};
/// A scenario identity with an optional embedded world file.
struct ResolvedScenario {
    std::string id;
    std::shared_ptr<const ResolvedFile> world; // Null means explicitly external.
};
/// The selected gameplay bundle and the pack that supplied it.
struct ResolvedGameplay {
    PackIdentity owner;
    std::shared_ptr<const ResolvedFile> manifest; // Null for the registered builtin.
    std::shared_ptr<const GameplayBundle> bundle;
};
/// An immutable content selection after inheritance, locale selection, and hash checks.
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
/// Budgets bounding inheritance depth, file count, and total resolved bytes.
struct ResolutionLimits {
    std::size_t maximum_chain = 64;
    std::size_t maximum_files = 4096;
    std::size_t maximum_bytes = 256 * 1024 * 1024;
};
/// Parents are explicit mounts addressed by exact manifest hash. Unselected
/// parent manifests and replaced component files are never decoded or opened.
std::shared_ptr<const ResolvedPack>
resolve_pack(const MountedPack& selected, std::span<const MountedPack> registered_parents = {},
             std::string_view requested_locale = {}, bool require_classic = false,
             ResolutionLimits limits = {});
/// Return the immutable gameplay bundle embedded in the application.
std::shared_ptr<const GameplayBundle> builtin_gameplay();
} // namespace openciv::formats::content
