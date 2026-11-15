#include <catch2/catch_test_macros.hpp>

import std;
import openciv.state.text_pool;

// F02 logical text ownership; no original allocation trace requirements.

using openciv::state::TextPool;
TEST_CASE("Text references resolve owned values across appends", "[text-pool]") {
    TextPool pool;
    REQUIRE_THROWS_AS(pool.text(0), std::out_of_range);
    pool.initialize();
    const auto first = pool.store("Alpha");
    const auto borrowed = pool.text(first);
    const auto duplicate = pool.store("Alpha");
    REQUIRE(pool.text(duplicate) == borrowed);
    REQUIRE(pool.text(pool.store("")) == "<nil>");
    for (unsigned i = 0; i < 70000; ++i)
        pool.store("Earlier");
    const auto later = pool.store("Later");
    REQUIRE(pool.text(later) == "Later");
    REQUIRE(pool.text(first) == "Alpha");
    REQUIRE(borrowed == "Alpha");
    REQUIRE(pool.text(pool.store(std::string("Before\0After", 12))) == "Before");
    pool.initialize();
    REQUIRE(pool.text(0) == "<nil>");
    REQUIRE_THROWS_AS(pool.text(later), std::out_of_range);
}
