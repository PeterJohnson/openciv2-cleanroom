// Authored manifest/selection expectations from data-format.md sections 2-3.
// Minimal component envelopes deliberately test resolution, not body validation.
#include "formats/content_pack.hpp"
#include "base/hash.hpp"
#include "builtin_gameplay_data.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

namespace {
using namespace openciv;
using namespace formats::content;
formats::Bytes bytes(std::string_view text) {
    return {text.begin(), text.end()};
}
std::string format(std::string_view component) {
    if (component == "cityNames")
        return "openciv2-city-names/1";
    if (component == "cityPreferences")
        return "openciv2-city-preferences/1";
    return "openciv2-" + std::string(component) + "/1";
}
struct Input {
    jt::Json manifest;
    std::map<std::string, formats::Bytes> files;
    std::vector<std::string> reads, scopes;
    Input(std::string name = "authored.root", bool root = true) {
        manifest =
            parse_json(R"({"format":"openciv2-content-pack/1","id":"authored.root","revision":1,
            "profile":"classic-mge-5.4.0f","defaultLocale":"en","base":null,"files":{},
            "provenance":{"authors":["Independent fixture author"],"license":"MIT","notes":[]}})",
                       "fixture")
                .value;
        manifest["id"] = jt::Json(name);
        if (!root)
            return;
        manifest["compatibility"] = jt::Json();
        manifest["scenarios"].setArray();
        manifest["resources"].setArray();
        manifest["gameplay"]["builtin"] = jt::Json("classic-mge-5.4.0f");
        manifest["gameplay"]["revision"] = jt::Json(1);
        for (const auto component : plain_components)
            document(component, {}, std::string(component) + ".json", "root");
        for (const auto component : localized_components)
            document(component, "en", std::string(component) + ".en.json", "root");
    }
    jt::Json source(std::string path, std::string_view raw) {
        files[path] = bytes(raw);
        jt::Json out;
        out["path"] = jt::Json(path);
        out["sha256"] = jt::Json(base::sha256(files[path]));
        return out;
    }
    void document(std::string_view component, std::string locale, std::string path,
                  std::string marker) {
        jt::Json value;
        value["format"] = jt::Json(format(component));
        value["marker"] = jt::Json(marker);
        if (!locale.empty())
            value["locale"] = jt::Json(locale);
        auto descriptor = source(path, value.toString());
        if (locale.empty())
            manifest["files"][std::string(component)] = descriptor;
        else
            manifest["files"][std::string(component)][locale] = descriptor;
    }
    void parent(const Input& parent) {
        manifest["base"]["id"] = parent.manifest["id"];
        manifest["base"]["revision"] = parent.manifest["revision"];
        manifest["base"]["sha256"] = jt::Json(base::sha256(bytes(parent.manifest.toString())));
    }
    MountedPack mount() {
        return {{manifest["id"].getString() + "/content.json", bytes(manifest.toString())},
                [this](std::string_view directory, std::string_view path) {
                    scopes.emplace_back(directory);
                    const auto full = directory.empty()
                                          ? std::string(path)
                                          : std::string(directory) + "/" + std::string(path);
                    reads.push_back(full);
                    return Source{manifest["id"].getString() + "/" + full, files.at(full)};
                }};
    }
};
template <class F> void error_code(std::string_view code, F run) {
    try {
        run();
        FAIL("expected content rejection");
    } catch (const Error& error) {
        INFO(error.what());
        REQUIRE(error.code == code);
    }
}
} // namespace

TEST_CASE("Content pack root resolves explicit files and builtin operands into owned receipts",
          "[content][pack]") {
    Input input;
    const auto original = input.files;
    const auto pack = resolve_pack(input.mount(), {}, "missing-language", true);
    REQUIRE(pack->manifests.size() == 1);
    REQUIRE(pack->manifests[0].pack.id == "authored.root");
    REQUIRE(pack->manifests[0].pack.revision == "1");
    REQUIRE(pack->manifests[0].provenance.license == "MIT");
    REQUIRE(pack->components.size() == 4);
    REQUIRE(pack->localized.size() == 5);
    REQUIRE(pack->selected_locale == "en");
    REQUIRE(pack->gameplay.bundle->semantic_sha256 == classic_gameplay_sha256);
    REQUIRE(pack->gameplay.bundle->values.calendar.profiles[0][0].start_year == -4000);
    REQUIRE_FALSE(pack->gameplay.manifest);
    REQUIRE_FALSE(pack->compatibility);
    REQUIRE(input.reads.size() == 9);
    REQUIRE(input.files == original);
    input.files.clear();
    REQUIRE(pack->components.at("rules").json.value["marker"].getString() == "root");
    REQUIRE_FALSE(pack->components.at("rules").file->bytes.empty());
}

TEST_CASE("Content overlays retain each origin and replace whole components lists and descriptors",
          "[content][pack]") {
    Input root;
    root.manifest["compatibility"] = root.source(
        "compatibility.json",
        R"({"format":"openciv2-classic-compatibility/1","profile":"classic-mge-5.4.0f"})");
    root.manifest["resources"].getArray().push_back(
        parse_json(
            R"({"id":"sound.parent","kind":"audio","path":"old.wav","sha256":"0000000000000000000000000000000000000000000000000000000000000000"})",
            "resource")
            .value);
    root.manifest["scenarios"].getArray().push_back(
        parse_json(R"({"id":"scenario.parent","world":null})", "scenario").value);
    Input middle("authored.middle", false);
    middle.parent(root);
    middle.document("rules", {}, "rules.json", "middle-only");
    for (const auto name : localized_components)
        middle.document(name, "fr", std::string(name) + ".fr.json", "fr-middle");
    middle.manifest["compatibility"] = jt::Json();
    middle.manifest["resources"].setArray();
    middle.manifest["scenarios"].setArray();
    Input leaf("authored.leaf", false);
    leaf.parent(middle);
    leaf.manifest["defaultLocale"] = jt::Json("fr");
    leaf.document("text", "fr", "text.fr.json", "leaf-only");
    auto root_mount = root.mount(), middle_mount = middle.mount();
    const std::array parents{root_mount, middle_mount};
    const auto pack = resolve_pack(leaf.mount(), parents, "fr");
    REQUIRE(pack->manifests.size() == 3);
    REQUIRE(pack->manifests[0].pack.id == "authored.root");
    REQUIRE(pack->manifests[2].pack.id == "authored.leaf");
    REQUIRE(pack->components.at("rules").file->owner.id == "authored.middle");
    REQUIRE(pack->components.at("rules").json.value["marker"].getString() == "middle-only");
    REQUIRE(pack->components.at("cityNames").file->owner.id == "authored.root");
    REQUIRE(pack->localized.at("text").at("fr").file->owner.id == "authored.leaf");
    REQUIRE(pack->localized.at("council").at("fr").file->owner.id == "authored.middle");
    REQUIRE(pack->localized.at("text").at("en").file->owner.id == "authored.root");
    REQUIRE(pack->selected_locale == "fr");
    REQUIRE(pack->resources.empty());
    REQUIRE(pack->scenarios.empty());
    REQUIRE_FALSE(pack->compatibility);
    REQUIRE(std::find(root.reads.begin(), root.reads.end(), "rules.json") == root.reads.end());
    REQUIRE(std::find(root.reads.begin(), root.reads.end(), "old.wav") == root.reads.end());
    REQUIRE(std::find(root.reads.begin(), root.reads.end(), "compatibility.json") ==
            root.reads.end());
    REQUIRE(std::find(middle.reads.begin(), middle.reads.end(), "text.fr.json") ==
            middle.reads.end());
    const std::array reversed{middle_mount, root_mount};
    const auto again = resolve_pack(leaf.mount(), reversed, "not-advertised");
    REQUIRE(again->selected_locale == "fr");
    REQUIRE(again->components.at("rules").file->receipt.sha256 ==
            pack->components.at("rules").file->receipt.sha256);
}

TEST_CASE(
    "Content parent discovery uses pinned bytes and diagnoses missing ambiguous or cyclic inputs",
    "[content][pack]") {
    Input root, leaf("authored.leaf", false);
    leaf.parent(root);
    error_code("missing-parent", [&] { resolve_pack(leaf.mount()); });
    REQUIRE(leaf.reads.empty());
    auto root_mount = root.mount();
    const MountedPack unrelated{{"unused", bytes("not JSON")}, {}};
    const std::array parents{unrelated, root_mount};
    REQUIRE(resolve_pack(leaf.mount(), parents)->manifests.size() == 2);
    const std::array duplicate{root_mount, root_mount};
    error_code("ambiguous-parent", [&] { resolve_pack(leaf.mount(), duplicate); });
    leaf.manifest["base"]["id"] = jt::Json("authored.different");
    error_code("parent-identity", [&] { resolve_pack(leaf.mount(), parents); });
    leaf.manifest["base"]["id"] = leaf.manifest["id"];
    error_code("dependency-cycle", [&] { resolve_pack(leaf.mount(), parents); });
    leaf.parent(root);
    // A cycle is diagnosed by active logical identities before trying a bogus
    // hash for a parent that would reenter the selected pack.
    root.manifest["base"] =
        parse_json(
            R"({"id":"authored.leaf","revision":1,"sha256":"0000000000000000000000000000000000000000000000000000000000000000"})",
            "base")
            .value;
    leaf.parent(root);
    const std::array cycle{root.mount()};
    error_code("dependency-cycle", [&] { resolve_pack(leaf.mount(), cycle); });
    leaf.manifest["base"]["sha256"] = jt::Json(base::sha256(unrelated.manifest.bytes));
    error_code("invalid-json", [&] { resolve_pack(leaf.mount(), parents); });
}

TEST_CASE("Content manifests reject invalid declarations before selected file reads",
          "[content][pack]") {
    const std::vector<std::function<void(Input&)>> changes{
        [](auto& i) { i.manifest.getObject().erase("gameplay"); },
        [](auto& i) { i.manifest.getObject().erase("resources"); },
        [](auto& i) { i.manifest["files"].getObject().erase("rules"); },
        [](auto& i) { i.manifest["files"]["text"].setObject(); },
        [](auto& i) { i.manifest["profile"] = jt::Json("invented"); },
        [](auto& i) { i.manifest["files"]["unknown"] = i.manifest["files"]["rules"]; },
        [](auto& i) { i.manifest["extensions"]["single"] = jt::Json(1); },
        [](auto& i) { i.manifest["gameplay"]["revision"] = jt::Json(2); },
        [](auto& i) { i.manifest["gameplay"]["extra"] = jt::Json(1); },
        [](auto& i) { i.manifest["files"]["rules"]["sha256"] = jt::Json("ABC"); },
        [](auto& i) {
            auto scenario = parse_json(R"({"id":"scenario.one","world":null})", "fixture").value;
            i.manifest["scenarios"].getArray() = {scenario, scenario};
        },
        [](auto& i) {
            auto resource = i.source("one.wav", "wave");
            resource["id"] = jt::Json("sound.one");
            resource["kind"] = jt::Json("audio");
            i.manifest["resources"].getArray() = {resource, resource};
        },
        [](auto& i) {
            auto resource = i.source("one.wav", "wave");
            resource["id"] = jt::Json("sound.one");
            resource["kind"] = jt::Json("audio");
            i.manifest["resources"].getArray().push_back(resource);
            resource["id"] = jt::Json("sound.two");
            i.manifest["resources"].getArray().push_back(resource);
        }};
    for (std::size_t n = 0; n < changes.size(); ++n) {
        INFO(n);
        Input input;
        changes[n](input);
        error_code("invalid-content", [&] { resolve_pack(input.mount()); });
        REQUIRE(input.reads.empty());
    }
    Input bad_path;
    bad_path.manifest["files"]["rules"]["path"] = jt::Json("../outside.json");
    error_code("invalid-path", [&] { resolve_pack(bad_path.mount()); });
    REQUIRE(bad_path.reads.empty());
}

TEST_CASE("Content locale selection rejects partial languages and mismatched envelopes",
          "[content][pack]") {
    Input root, leaf("authored.leaf", false);
    leaf.parent(root);
    leaf.document("text", "fr", "text.fr.json", "fragment");
    const std::array parents{root.mount()};
    error_code("incomplete-locale", [&] { resolve_pack(leaf.mount(), parents, "fr"); });
    REQUIRE(root.reads.empty());
    REQUIRE(leaf.reads.empty());
    leaf = Input("authored.leaf", false);
    leaf.parent(root);
    leaf.manifest["defaultLocale"] = jt::Json("absent");
    error_code("incomplete-locale", [&] { resolve_pack(leaf.mount(), parents); });
    root.manifest["files"]["text"]["en"] =
        root.source("text.en.json", R"({"format":"openciv2-text/1","locale":"fr"})");
    error_code("invalid-content", [&] { resolve_pack(root.mount()); });
    root.manifest["files"]["text"]["en"] =
        root.source("text.en.json", R"({"format":"openciv2-text/2","locale":"en"})");
    error_code("invalid-content", [&] { resolve_pack(root.mount()); });
}

TEST_CASE("Content hashes precede JSON decoding and worlds resources retain exact owned bytes",
          "[content][pack]") {
    Input root;
    root.files["rules.json"] = bytes("not JSON");
    error_code("hash-mismatch", [&] { resolve_pack(root.mount()); });
    root.manifest["files"]["rules"] = root.source("rules.json", "not JSON");
    error_code("invalid-json", [&] { resolve_pack(root.mount()); });
    root = Input{};
    jt::Json scenario;
    scenario["id"] = jt::Json("scenario.one");
    scenario["world"] = root.source("one.scn", std::string("\0\1\xff", 3));
    root.manifest["scenarios"].getArray().push_back(scenario);
    scenario["id"] = jt::Json("scenario.external");
    scenario["world"] = jt::Json();
    root.manifest["scenarios"].getArray().push_back(scenario);
    auto resource = root.source("one.wav", "raw audio input");
    resource["id"] = jt::Json("sound.one");
    resource["kind"] = jt::Json("audio");
    root.manifest["resources"].getArray().push_back(resource);
    root.manifest["compatibility"] = root.source(
        "compatibility.json",
        R"({"format":"openciv2-classic-compatibility/1","profile":"classic-mge-5.4.0f"})");
    const auto pack = resolve_pack(root.mount());
    REQUIRE(pack->scenarios.size() == 2);
    REQUIRE(pack->scenarios[0].world->bytes == formats::Bytes{0, 1, 255});
    REQUIRE_FALSE(pack->scenarios[1].world);
    REQUIRE(pack->resources[0].file->bytes == bytes("raw audio input"));
    REQUIRE(pack->resources[0].kind == ResourceKind::audio);
    REQUIRE(pack->compatibility.has_value());
    root.files.erase("one.wav");
    error_code("source-error", [&] { resolve_pack(root.mount()); });
}

TEST_CASE("Content nested gameplay uses its owner and scoped directory with classic gate",
          "[content][pack]") {
    Input root;
    jt::Json gameplay;
    for (const auto& [path, data] : builtin_gameplay_files) {
        root.files["variant/" + std::string(path)] = bytes(data);
        if (path == "gameplay.json")
            gameplay = parse_json(data, "fixture").value;
    }
    auto combat = parse_json(builtin_gameplay_files[5].second, "combat").value;
    combat["antiAirMultipliers"]["ordinaryAircraft"] = jt::Json(7);
    const auto changed = root.source("variant/combat.json", combat.toString());
    gameplay["mode"] = jt::Json("custom");
    gameplay["components"]["combat"]["sha256"] = changed["sha256"];
    root.manifest["gameplay"] = root.source("variant/gameplay.json", gameplay.toString());
    Input leaf("authored.leaf", false);
    leaf.parent(root);
    const std::array parents{root.mount()};
    const auto pack = resolve_pack(leaf.mount(), parents);
    REQUIRE(pack->gameplay.owner.id == "authored.root");
    REQUIRE(pack->gameplay.manifest->receipt.path == "variant/gameplay.json");
    REQUIRE(pack->gameplay.bundle->values.combat.ordinary_aircraft_multiplier == 7);
    REQUIRE_FALSE(pack->gameplay.bundle->classic_values);
    REQUIRE(std::count(root.scopes.begin(), root.scopes.end(), "variant") == 6);
    REQUIRE(leaf.reads.empty());
    error_code("classic-values-required", [&] { resolve_pack(leaf.mount(), parents, {}, true); });
    leaf.manifest["gameplay"]["builtin"] = jt::Json("classic-mge-5.4.0f");
    leaf.manifest["gameplay"]["revision"] = jt::Json(1);
    root.reads.clear();
    REQUIRE(resolve_pack(leaf.mount(), parents, {}, true)->gameplay.bundle->classic_values);
    REQUIRE(std::find(root.reads.begin(), root.reads.end(), "variant/gameplay.json") ==
            root.reads.end());
}

TEST_CASE("Content resolution bounds work and preserves exact revision metadata",
          "[content][pack]") {
    Input root, leaf("authored.leaf", false);
    root.manifest["revision"] = parse_json("9007199254740993", "revision").value;
    leaf.parent(root);
    const std::array parents{root.mount()};
    REQUIRE(resolve_pack(leaf.mount(), parents)->manifests[0].pack.revision == "9007199254740993");
    ResolutionLimits limits;
    limits.maximum_chain = 1;
    error_code("resource-limit", [&] { resolve_pack(leaf.mount(), parents, {}, false, limits); });
    limits = {};
    limits.maximum_files = 8;
    error_code("resource-limit", [&] { resolve_pack(root.mount(), {}, {}, false, limits); });
    limits = {};
    limits.maximum_bytes = root.mount().manifest.bytes.size() - 1;
    root.reads.clear();
    error_code("resource-limit", [&] { resolve_pack(root.mount(), {}, {}, false, limits); });
    REQUIRE(root.reads.empty());
}
