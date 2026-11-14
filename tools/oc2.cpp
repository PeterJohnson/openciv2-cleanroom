#include "platform/native_files.hpp"
#include "content_command.hpp"
#include "query/output.hpp"
#include "base/cancellation.hpp"
#ifdef OC2_ENABLE_SQLITE_EXPORT
#include "analysis/sqlite_export.hpp"
#endif
#include <cctype>
#include <csignal>
#include <iostream>

namespace {
using namespace openciv;
constexpr auto usage =
    "Usage: oc2 inspect FILE [--json] [--cities|--units|--tiles|--civilizations|--section NAME]\n"
    "       oc2 validate FILE [--json]\n"
    "       oc2 diff BEFORE AFTER [--json]\n"
    "       oc2 content validate GAMEPLAY_MANIFEST [--require-classic] [--json]\n"
    "       oc2 content resolve CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] "
    "[--require-classic] [--json]\n"
    "       oc2 content validate-rules CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] "
    "[--require-classic] [--json]\n"
    "       oc2 content convert-names CITY_SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent "
    "MANIFEST]...\n"
    "       oc2 content convert-preferences CITYPREF_SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent "
    "MANIFEST]...\n"
    "       oc2 content validate-preferences CONTENT_MANIFEST [--parent MANIFEST]... [--locale "
    "LOCALE] "
    "[--require-classic] [--json]\n"
    "       oc2 content validate-names CONTENT_MANIFEST [--parent MANIFEST]... [--locale LOCALE] "
    "[--require-classic] [--json]\n"
    "       oc2 export FILE --sqlite NEW_DATABASE [--json] (optional build feature)\n"
    "Options: --format save|map (default: .mp means map; all others mean save)\n"
    "         --view stored (loaded/live views are not implemented)\n"
    "Stored save support: version 0x2c. Inputs are read-only.\n"
    "Exit: 0 checks passed/equal; 1 semantic errors/different; 2 usage/I/O failure;\n"
    "      3 unsupported feature/version/view/comparison; 4 malformed input; 130 cancelled.\n";
#ifdef OC2_ENABLE_SQLITE_EXPORT
volatile std::sig_atomic_t interrupted = 0;
void interrupt_export(int) {
    interrupted = 1;
}
class ExportSignals {
public:
    ExportSignals() {
        interrupted = 0;
        previous_int_ = std::signal(SIGINT, interrupt_export);
        previous_term_ = std::signal(SIGTERM, interrupt_export);
    }
    ~ExportSignals() {
        std::signal(SIGINT, previous_int_);
        std::signal(SIGTERM, previous_term_);
    }

private:
    using Handler = void (*)(int);
    Handler previous_int_, previous_term_;
};
#endif
query::FileKind file_kind(const std::filesystem::path& path,
                          const std::optional<query::FileKind>& selected) {
    if (selected)
        return *selected;
    auto suffix = path.extension().u8string();
    std::transform(suffix.begin(), suffix.end(), suffix.begin(),
                   [](unsigned char c) { return static_cast<char8_t>(std::tolower(c)); });
    return suffix == u8".mp" ? query::FileKind::raw_map : query::FileKind::save;
}
int status(const query::StoredSnapshot& snapshot) {
    if (snapshot.unsupported())
        return 3;
    if (!snapshot.decoded())
        return 4;
    return snapshot.has_errors() ? 1 : 0;
}
void flush_output() {
    std::cout.flush();
    if (!std::cout)
        throw std::runtime_error("Output write failed");
}
int run(const std::vector<std::string>& args) {
    try {
        if (args.empty() || args[0] == "--help" || args[0] == "help") {
            std::cout << usage;
            flush_output();
            return args.empty() ? 2 : 0;
        }
        const auto& command = args[0];
        if (command == "content")
            return run_content_command(std::span(args).subspan(1));
        if (command != "inspect" && command != "validate" && command != "diff" &&
            command != "export")
            throw std::invalid_argument("Unknown command");
        bool json = false, positional_only = false;
        std::vector<std::filesystem::path> paths;
        std::optional<query::FileKind> format;
        std::optional<std::filesystem::path> destination;
        query::OutputSelection selection;
        for (std::size_t i = 1; i < args.size(); ++i) {
            const auto& arg = args[i];
            if (positional_only || !arg.starts_with("--")) {
                paths.emplace_back(std::u8string{arg.begin(), arg.end()});
                continue;
            }
            if (arg == "--") {
                positional_only = true;
                continue;
            }
            if (arg == "--json") {
                json = true;
                continue;
            }
            if (arg == "--help") {
                std::cout << usage;
                flush_output();
                return 0;
            }
            const auto next = [&]() -> const std::string& {
                if (++i >= args.size())
                    throw std::invalid_argument("Missing option value");
                return args[i];
            };
            if (arg == "--sqlite") {
                if (command != "export" || destination)
                    throw std::invalid_argument("--sqlite is valid once for export only");
                const auto& value = next();
                destination.emplace(std::u8string{value.begin(), value.end()});
                continue;
            }
            if (arg == "--view") {
                if (next() != "stored") {
                    std::cerr << "Only stored inspection is implemented; loaded/live views require "
                                 "session loading\n";
                    return 3;
                }
                continue;
            }
            if (arg == "--format") {
                const auto& value = next();
                if (value == "save")
                    format = query::FileKind::save;
                else if (value == "map")
                    format = query::FileKind::raw_map;
                else
                    throw std::invalid_argument("Format must be save or map");
                continue;
            }
            const std::string section = arg == "--section" ? next() : arg.substr(2);
            bool found = false;
            for (std::size_t j = 0; j <= static_cast<std::size_t>(query::Section::trailing); ++j) {
                const auto candidate = static_cast<query::Section>(j);
                if (section == query::name(candidate)) {
                    selection.sections.push_back(candidate);
                    found = true;
                    break;
                }
            }
            if (!found)
                throw std::invalid_argument("Unknown option or section");
        }
        if (paths.size() != (command == "diff" ? 2U : 1U))
            throw std::invalid_argument("Incorrect number of input paths");
        if (command != "inspect" && !selection.sections.empty())
            throw std::invalid_argument("Section selection is available only for inspect");
        if (command == "export" && !destination)
            throw std::invalid_argument("Export requires --sqlite NEW_DATABASE");
#ifndef OC2_ENABLE_SQLITE_EXPORT
        if (command == "export") {
            std::cerr << "SQLite export is unavailable; configure OC2_ENABLE_SQLITE_EXPORT=ON\n";
            return 3;
        }
#endif
        const auto before =
            query::inspect_stored(io::read_file(paths[0]), file_kind(paths[0], format),
                                  query::FileAccess::offline_omniscient);
        auto result = status(before);
        if (command == "export") {
#ifdef OC2_ENABLE_SQLITE_EXPORT
            if (!before.decoded()) {
                if (json)
                    query::write_snapshot_json(std::cout, before, {}, false);
                else
                    query::write_snapshot_text(std::cout, before);
            } else {
                ExportSignals signals;
                std::stop_source cancellation;
                const auto progress = [&](std::size_t) {
                    if (interrupted)
                        cancellation.request_stop();
                };
                const auto bytes =
                    analysis::sqlite_snapshot(before, cancellation.get_token(), progress);
                io::write_new_file(*destination, bytes, cancellation.get_token(), progress);
                if (json)
                    std::cout << "{\"schema\":\"openciv2-export-receipt/1\",\"complete\":true,"
                                 "\"analysisSchema\":\"openciv2-analysis/1\",\"sourceSha256\":\""
                              << before.source_hash() << "\",\"bytes\":\"" << bytes.size() << "\"}";
                else
                    std::cout << "Exported SQLite analysis (schema 1); " << bytes.size()
                              << " bytes; source SHA-256 " << before.source_hash()
                              << "\nValidation coverage remains incomplete; inspect database "
                                 "diagnostics.";
            }
#endif
        } else if (command == "diff") {
            const auto after =
                query::inspect_stored(io::read_file(paths[1]), file_kind(paths[1], format),
                                      query::FileAccess::offline_omniscient);
            const auto diff = query::diff_stored(before, after);
            if (json)
                query::write_diff_json(std::cout, before, after, diff);
            else
                query::write_diff_text(std::cout, before, after, diff);
            result = std::max(result, status(after));
            if (result < 3 && !diff.fields_compared)
                result = 3;
            if (result == 0 && diff.different())
                result = 1;
        } else {
            if (json)
                query::write_snapshot_json(std::cout, before, selection, command == "inspect");
            else
                query::write_snapshot_text(std::cout, before, selection,
                                           command == "inspect" && !selection.sections.empty());
        }
        std::cout << '\n';
        flush_output();
        return result;
    } catch (const base::Cancelled& error) {
        std::cerr << error.what() << '\n';
        return 130;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n' << usage;
        return 2;
    }
}
} // namespace
#ifdef _WIN32
int wmain(int argc, wchar_t** argv) {
    std::vector<std::string> args;
    for (int i = 1; i < argc; ++i) {
        const auto value = std::filesystem::path{argv[i]}.u8string();
        args.emplace_back(value.begin(), value.end());
    }
    return run(args);
}
#else
int main(int argc, char** argv) {
    std::signal(SIGPIPE, SIG_IGN);
    return run({argv + 1, argv + argc});
}
#endif
