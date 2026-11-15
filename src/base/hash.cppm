export module openciv.base.hash;

import std;

export namespace openciv::base {
/// Encode bytes as lowercase hexadecimal, with two characters per byte.
/// Content identities only; no gameplay random context or host services.
std::string hex_bytes(std::span<const std::uint8_t> bytes);
/// Return the lowercase hexadecimal SHA-256 digest of the supplied bytes.
std::string sha256(std::span<const std::uint8_t> bytes);
} // namespace openciv::base

module :private;
namespace openciv::base {
namespace {
// FIPS 180-4 §§4.2.2,5.1.1,5.3.3,6.2.2. Independently written implementation.
// https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.180-4.pdf
constexpr std::array<std::uint32_t, 64> constants{
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2};
void block(std::array<std::uint32_t, 8>& hash, std::span<const std::uint8_t, 64> bytes) {
    std::array<std::uint32_t, 64> words{};
    for (std::size_t i = 0; i < 16; ++i)
        for (std::size_t j = 0; j < 4; ++j)
            words[i] = (words[i] << 8) | bytes[4 * i + j];
    for (std::size_t i = 16; i < 64; ++i) {
        const auto x = words[i - 15], y = words[i - 2];
        const auto small0 = std::rotr(x, 7) ^ std::rotr(x, 18) ^ (x >> 3);
        const auto small1 = std::rotr(y, 17) ^ std::rotr(y, 19) ^ (y >> 10);
        words[i] = words[i - 16] + small0 + words[i - 7] + small1;
    }
    auto [a, b, c, d, e, f, g, h] = hash;
    for (std::size_t i = 0; i < 64; ++i) {
        const auto big1 = std::rotr(e, 6) ^ std::rotr(e, 11) ^ std::rotr(e, 25);
        const auto choose = (e & f) ^ (~e & g);
        const auto first = h + big1 + choose + constants[i] + words[i];
        const auto big0 = std::rotr(a, 2) ^ std::rotr(a, 13) ^ std::rotr(a, 22);
        const auto majority = (a & b) ^ (a & c) ^ (b & c);
        h = g;
        g = f;
        f = e;
        e = d + first;
        d = c;
        c = b;
        b = a;
        a = first + big0 + majority;
    }
    const std::array result{a, b, c, d, e, f, g, h};
    for (std::size_t i = 0; i < hash.size(); ++i)
        hash[i] += result[i];
}
} // namespace
std::string hex_bytes(std::span<const std::uint8_t> bytes) {
    constexpr char digits[] = "0123456789abcdef";
    std::string result;
    if (bytes.size() > result.max_size() / 2)
        throw std::length_error("Hexadecimal output too large");
    result.reserve(bytes.size() * 2);
    for (auto byte : bytes) {
        result.push_back(digits[byte >> 4]);
        result.push_back(digits[byte & 15]);
    }
    return result;
}
std::string sha256(std::span<const std::uint8_t> bytes) {
    if (static_cast<std::uint64_t>(bytes.size()) > std::numeric_limits<std::uint64_t>::max() / 8)
        throw std::length_error("SHA-256 input exceeds its bit-length domain");
    const auto bits = static_cast<std::uint64_t>(bytes.size()) * 8;
    std::array<std::uint32_t, 8> hash{0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
                                      0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19};
    while (bytes.size() >= 64) {
        block(hash, bytes.first<64>());
        bytes = bytes.subspan(64);
    }
    std::array<std::uint8_t, 128> tail{};
    std::copy(bytes.begin(), bytes.end(), tail.begin());
    tail[bytes.size()] = 0x80;
    const std::size_t end = bytes.size() < 56 ? 64 : 128;
    for (std::size_t i = 0; i < 8; ++i)
        tail[end - 1 - i] = static_cast<std::uint8_t>(bits >> (8 * i));
    block(hash, std::span{tail}.first<64>());
    if (end == 128)
        block(hash, std::span{tail}.last<64>());
    std::array<std::uint8_t, 32> digest{};
    for (std::size_t i = 0; i < hash.size(); ++i)
        for (std::size_t j = 0; j < 4; ++j)
            digest[4 * i + j] = static_cast<std::uint8_t>(hash[i] >> (24 - 8 * j));
    return hex_bytes(digest);
}
} // namespace openciv::base
