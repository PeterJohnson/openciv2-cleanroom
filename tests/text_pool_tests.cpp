// F02 section 8 / F03 Appendix A / P04 section 2, authored expectations.
#include "state/text_pool.hpp"
#include <catch2/catch_test_macros.hpp>
#include <new>
#include <stdexcept>

using openciv::state::TextPool;
TEST_CASE("Shared text pool keeps ordinal identities padding empty behavior and stable text",
          "[text-pool]") {
    TextPool pool;
    REQUIRE(pool.size() == 0);
    REQUIRE_THROWS_AS(pool.text(0), std::out_of_range);
    pool.initialize();
    REQUIRE(pool.size() == 1);
    REQUIRE(pool.text(0) == "<nil>");
    REQUIRE(pool.allocation(0) == std::string("<nil>\0", 6));
    REQUIRE(pool.last() == 0);
    const auto first = pool.store("Alpha");
    const auto borrowed = pool.text(first);
    REQUIRE(first == 1);
    REQUIRE(pool.store("Alpha") == 2); // No deduplication.
    REQUIRE(pool.store("") == 0);
    REQUIRE(pool.last() == 2);
    REQUIRE(pool.size() == 3);
    REQUIRE(pool.store("", 20) == 3);
    REQUIRE(pool.text(3) == " ");
    REQUIRE(pool.allocation(3) == " " + std::string(19, '\0'));
    REQUIRE(pool.store("Longer than minimum", 3) == 4);
    REQUIRE(pool.allocation(4).size() == 20);
    for (unsigned i = 0; i < 1000; ++i)
        pool.store("Different backing");
    REQUIRE(borrowed == "Alpha");
    REQUIRE(pool.text(first).data() == borrowed.data());
    REQUIRE(pool.store(std::string("Before\0After", 12)) == 1005);
    REQUIRE(pool.text(1005) == "Before");
    REQUIRE(openciv::state::label_magnitude(-32768) == 32768);
    REQUIRE(openciv::state::label_magnitude(-1) == 1);
}
TEST_CASE("Text allocation failure preserves the append or reset prefix", "[text-pool]") {
    TextPool pool;
    pool.initialize();
    pool.store("Retained");
    const auto fail = [](std::string_view, std::size_t) { throw std::bad_alloc(); };
    REQUIRE_THROWS_AS(pool.store("Next", 24, fail), std::bad_alloc);
    REQUIRE(pool.size() == 2);
    REQUIRE(pool.last() == 1);
    REQUIRE(pool.text(1) == "Retained");
    // Empty ordinary text never reaches the allocator, even one that fails.
    REQUIRE(pool.store("", 0, fail) == 0);
    REQUIRE_THROWS_AS(pool.initialize(fail), std::bad_alloc);
    REQUIRE(pool.size() == 0);
    REQUIRE_FALSE(pool.last());
    REQUIRE_THROWS_AS(pool.text(1), std::out_of_range);
    pool.initialize();
    REQUIRE(pool.store("Restarted") == 1);
}
TEST_CASE("Text pooling captures input but assigns ordinals from post-callback state",
          "[text-pool]") {
    TextPool pool;
    pool.initialize();
    pool.store("Captured");
    const auto id = pool.store(pool.text(1), 15, [&](std::string_view bytes, std::size_t size) {
        REQUIRE(bytes == "Captured");
        REQUIRE(size == 15);
        pool.initialize();
        pool.store("Inserted");
        REQUIRE(bytes == "Captured");
    });
    REQUIRE(id == 2);
    REQUIRE(pool.last() == 2);
    REQUIRE(pool.text(1) == "Inserted");
    REQUIRE(pool.text(2) == "Captured");
}
