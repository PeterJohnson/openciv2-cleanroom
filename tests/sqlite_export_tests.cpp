#include <catch2/catch_test_macros.hpp>
#include <catch2/matchers/catch_matchers.hpp>

import std;
import openciv.analysis.sqlite_export;
import openciv.base.cancellation;

using namespace openciv;
TEST_CASE("SQLite export cancellation and failure discard analysis without changing source",
          "[sqlite]") {
    formats::RawMapFile map;
    map.header = {4, 2, 4, 0, 24, 1, 1};
    map.tiles.resize(4);
    const auto bytes = formats::encode_raw_map(map);
    const auto snapshot = query::inspect_stored(bytes, query::FileKind::raw_map,
                                                query::FileAccess::offline_omniscient);
    REQUIRE(snapshot.decoded());
    const auto before = snapshot.records();
    const auto expected = analysis::sqlite_snapshot(snapshot);
    CHECK(expected.size() > 4096);
    CHECK(std::string(expected.begin(), expected.begin() + 15) == "SQLite format 3");
    base::CancellationSource cancellation;
    std::size_t observed = 0;
    CHECK_THROWS_AS(analysis::sqlite_snapshot(snapshot, cancellation.get_token(),
                                              [&](std::size_t completed) {
                                                  observed = completed;
                                                  if (completed == 2)
                                                      cancellation.request_stop();
                                              }),
                    base::Cancelled);
    CHECK(observed == 2);
    CHECK_THROWS_AS(analysis::sqlite_snapshot(
                        snapshot, cancellation.get_token(),
                        [](std::size_t) { FAIL("Pre-cancelled export invoked progress"); }),
                    base::Cancelled);
    CHECK_THROWS_WITH(analysis::sqlite_snapshot(snapshot, {},
                                                [](std::size_t completed) {
                                                    if (completed == 2)
                                                        throw std::runtime_error(
                                                            "injected export failure");
                                                }),
                      "injected export failure");
    CHECK(snapshot.records() == before);
    CHECK(formats::Bytes(snapshot.source_bytes().begin(), snapshot.source_bytes().end()) == bytes);
    CHECK(analysis::sqlite_snapshot(snapshot) == expected);
    const auto invalid =
        query::inspect_stored({}, query::FileKind::save, query::FileAccess::offline_omniscient);
    CHECK_THROWS_AS(analysis::sqlite_snapshot(invalid), std::invalid_argument);
}
