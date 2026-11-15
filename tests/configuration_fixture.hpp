#pragma once
#include "service_callbacks.hpp"
#include "classic_stream_fixture.hpp"

#include "session/configuration.hpp"
#include "session/runtime.hpp"
#include "formats/preference_io.hpp"
#include <catch2/catch_test_macros.hpp>

namespace openciv::test {
// Authored F03 file outcomes shared by configuration callers. The real writer
// serializes the two process records; this service captures each actual item.
struct ConfigurationFixture {
    state::ProcessContext process;
    session::SessionRuntime runtime{process, base::RuntimeId{79}};
    session::PreferenceFileCallbacks preferences;
    std::vector<std::string> trace;
    std::vector<formats::Bytes> written;
    bool fail_open = false;
    std::optional<std::size_t> fail_item;
    std::function<void()> after_open, after_close;
    std::function<void(std::size_t)> after_write;
    ConfigurationFixture() {
        runtime.begin_client_session({10, 20});
        process.ordinary_preferences.game_options = 0xdeadbeefU;
        process.ordinary_preferences.disabled_messages = 0x9876;
        preferences.open_output =
            [&](std::string_view path) -> std::unique_ptr<session::CoreOutput> {
            CHECK(path == "CIV2.DAT");
            trace.push_back("open");
            if (after_open)
                after_open();
            if (fail_open)
                return {};
            return test::output_file({[&](std::span<const formats::Byte> bytes) {
                                          const auto index = written.size();
                                          trace.push_back("write:" + std::to_string(bytes.size()));
                                          written.emplace_back(bytes.begin(), bytes.end());
                                          if (after_write)
                                              after_write(index);
                                          return fail_item != index;
                                      },
                                      [&] {
                                          trace.push_back("close");
                                          if (after_close)
                                              after_close();
                                          return true;
                                      }});
        };
        preferences.remove_file = [&](std::string_view path) {
            CHECK(path == "CIV2.DAT");
            trace.push_back("delete");
            return false;
        };
    }
};
} // namespace openciv::test
