export module openciv.state.world_lifetime;

export namespace openciv::state {
/// A shared lifetime marker used to detect replacement of world-owned state.
struct WorldLifetime {};
} // namespace openciv::state
