import std;
import openciv.base.arithmetic;

// Standard-library module integration, including the F01 §1 arithmetic type.
// No standard-library headers are included in this consumer.
int main() {
    using openciv::base::WrappingI32;
    std::vector<WrappingI32> values{WrappingI32{3}, WrappingI32{1}, WrappingI32{2}};
    std::ranges::sort(values);
    if (values.front() != WrappingI32{1} || values.back() != WrappingI32{3})
        return 1;
    const auto sum = std::accumulate(values.begin(), values.end(), WrappingI32{0});
    const std::string message = "sum=" + std::to_string(static_cast<std::int32_t>(sum));
    if (message != "sum=6")
        return 2;
    try {
        (void)openciv::base::divide32(1, 0);
        return 3;
    } catch (const std::domain_error&) {
    }
    return 0;
}
