#include <catch2/catch_test_macros.hpp>
#include <catch2/matchers/catch_matchers.hpp>

import std;
import openciv.platform.native_files;
import openciv.platform.content_files;
import openciv.base.cancellation;

using namespace openciv;
namespace {
class TemporaryDirectory {
public:
    TemporaryDirectory() {
        const auto stamp = std::chrono::steady_clock::now().time_since_epoch().count();
        for (int i = 0; i < 100; ++i) {
            path = std::filesystem::temp_directory_path() /
                   ("oc2-publication-" + std::to_string(stamp) + "-" + std::to_string(i));
            if (std::filesystem::create_directory(path))
                return;
        }
        throw std::runtime_error("Cannot reserve test directory");
    }
    ~TemporaryDirectory() {
        std::error_code ignored;
        std::filesystem::remove_all(path, ignored);
    }
    std::filesystem::path path;
};
} // namespace
TEST_CASE("Native content mount enforces nested directory and byte boundaries", "[io][content]") {
    TemporaryDirectory directory;
    std::filesystem::create_directory(directory.path / "bundle");
    io::write_new_file(directory.path / "bundle" / "inside.json", formats::Bytes{1, 2, 3});
    io::write_new_file(directory.path / "outside.json", formats::Bytes{4, 5, 6});
    io::ContentDirectory mount(directory.path);
    CHECK(mount.read("bundle/inside.json", 3, "bundle").bytes == formats::Bytes{1, 2, 3});
    CHECK_THROWS(mount.read("outside.json", 3, "bundle"));
    CHECK_THROWS(mount.read("bundle/inside.json", 2, "bundle"));
    CHECK_THROWS(mount.read("bundle/inside.json", 0));
    CHECK_THROWS(mount.read("bundle/inside.json", 3, "../escape"));
    CHECK(mount.read("outside.json", 3).bytes == formats::Bytes{4, 5, 6});
}
TEST_CASE("Native publication never overwrites an existing or concurrent destination", "[io]") {
    TemporaryDirectory directory;
    const auto target = directory.path / "snapshot.sqlite";
    const formats::Bytes original{1, 2, 3};
    io::write_new_file(target, original);
    CHECK(io::read_file(target) == original);
    CHECK_THROWS(io::write_new_file(target, formats::Bytes{4, 5}));
    CHECK(io::read_file(target) == original);
    const auto race = directory.path / "race.sqlite";
    bool created = false;
    auto create_competing_file = [&](std::size_t written) {
        if (written != 0 && !created) {
            created = true;
            io::write_new_file(race, original);
        }
    };
    CHECK_THROWS(io::write_new_file(race, formats::Bytes{8}, {}, create_competing_file));
    CHECK(io::read_file(race) == original);
    CHECK(std::distance(std::filesystem::directory_iterator(directory.path),
                        std::filesystem::directory_iterator{}) == 2);
}
TEST_CASE("Native failed or cancelled staged writes publish no destination", "[io]") {
    TemporaryDirectory directory;
    const auto target = directory.path / "snapshot.sqlite";
    const formats::Bytes bytes(200000, 0xff);
    base::CancellationSource cancellation;
    auto cancel_after_write = [&](std::size_t written) {
        CHECK_FALSE(std::filesystem::exists(target));
        if (written != 0)
            cancellation.request_stop();
    };
    CHECK_THROWS_AS(io::write_new_file(target, bytes, cancellation.get_token(), cancel_after_write),
                    base::Cancelled);
    CHECK(std::filesystem::is_empty(directory.path));
    auto fail_after_write = [&](std::size_t written) {
        if (written != 0)
            throw std::runtime_error("injected write-stage failure");
    };
    CHECK_THROWS_WITH(io::write_new_file(target, bytes, {}, fail_after_write),
                      "injected write-stage failure");
    CHECK(std::filesystem::is_empty(directory.path));
    CHECK_THROWS(io::write_new_file(directory.path / "missing" / "file.sqlite", bytes));
    CHECK(std::filesystem::is_empty(directory.path));
    io::write_new_file(target, bytes);
    CHECK(io::read_file(target) == bytes);
}
