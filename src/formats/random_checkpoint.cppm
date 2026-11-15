export module openciv.formats.random_checkpoint;

import std;
export import openciv.base.random;

export namespace openciv::formats {
// M1-01 component, not a whole-session checkpoint or classic file/wire field.
// Standard engine restoration is linear in draws, explicitly bounded by caller.
// Codec v1 caps JSON at 16 MiB, total tape/trace records at 16384, key strings at
// 4096 bytes and trace capacity at u32. Unsupported raw sources reject.
std::string encode_random_checkpoint(const base::RandomCheckpoint& checkpoint,
                                     std::uint64_t replay_limit = 1000000);
base::RandomCheckpoint decode_random_checkpoint(std::string_view json,
                                                std::uint64_t replay_limit = 1000000);
} // namespace openciv::formats
