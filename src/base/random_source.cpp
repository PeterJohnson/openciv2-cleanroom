#include "base/random_source.hpp"
#include "base/random.hpp"
#include <random>

namespace openciv::base {
namespace {
class Mt19937Source final : public RandomSource {
public:
    explicit Mt19937Source(std::uint64_t seed) : engine_(seed) {}
    std::string_view id() const noexcept override { return random_policy_id; }
    std::uint64_t next() override { return engine_(); }
    std::unique_ptr<RandomSource> clone() const override {
        return std::make_unique<Mt19937Source>(*this);
    }
    bool same_state(const RandomSource& other) const noexcept override {
        const auto* source = dynamic_cast<const Mt19937Source*>(&other);
        return source && engine_ == source->engine_;
    }

private:
    std::mt19937_64 engine_;
};
} // namespace
void RandomSource::discard(std::uint64_t count) {
    while (count-- != 0)
        next();
}
std::unique_ptr<RandomSource> make_mt19937_source(std::uint64_t seed) {
    return std::make_unique<Mt19937Source>(seed);
}
} // namespace openciv::base
