#include "session/premade_world.hpp"
#include "session/runtime.hpp"
#include <algorithm>

namespace openciv::session {
void PremadeWorldChoices::validate() const {
    if (!use_seed || !use_start_locations || !ticks)
        throw std::invalid_argument("Premade world requires seed, starts and clock services");
}
void PremadeWorldServices::validate() const {
    if (!open || !load_failure)
        throw std::invalid_argument("Premade world requires input and failure services");
    choices.validate();
}
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
template <class Operation> auto complete(SessionRuntime& runtime, Operation operation) {
    try {
        const auto result = operation();
        publish(runtime);
        return result;
    } catch (...) {
        publish(runtime);
        throw;
    }
}
void normalize(world::MapStorage& map) {
    if (!map.allocated() || map.tiles().size() != map.geometry().area())
        throw std::invalid_argument("Premade preparation requires complete allocated map backing");
    for (auto& tile : map.tiles()) {
        tile.features = 0;
        tile.claimant = 0;
        tile.radius_other = 0;
        tile.revelation = 0;
        tile.region = 0;
        tile.owner.reset();
        tile.fertility = 0;
    }
    for (unsigned civilization = 1; civilization < 8; ++civilization) {
        const auto layer = map.remembered_layer(civilization);
        if (layer.size() != map.geometry().area())
            throw std::invalid_argument("Premade remembered layer has incomplete backing");
        std::fill(layer.begin(), layer.end(), 0);
    }
}
} // namespace
WorldSource prepare_premade_world(SessionRuntime& runtime, formats::StartingPositions& starts,
                                  const PremadeWorldChoices& supplied) {
    const auto choices = supplied;
    choices.validate();
    return complete(runtime, [&] {
        auto& map = runtime.world().map;
        normalize(map);
        bool replace_seed = map.pattern_seed < 2;
        if (!replace_seed) {
            publish(runtime);
            replace_seed = choices.use_seed() == 0;
        }
        if (replace_seed) {
            publish(runtime);
            map.pattern_seed = base::map_pattern_seed_from_ticks(choices.ticks());
        }
        if (std::any_of(starts.x.begin(), starts.x.end(), [](auto x) { return x >= 0; })) {
            publish(runtime);
            if (choices.use_start_locations() == 0)
                starts = {};
        }
        auto& rules = runtime.world().globals.rules;
        rules &= 0x7fffU;
        if (map.stored_topology != 0) {
            rules |= 0x8000U;
            return WorldSource::premade_flat;
        }
        return WorldSource::premade_cylindrical;
    });
}
std::optional<WorldSource> load_premade_world(SessionRuntime& runtime,
                                              formats::StartingPositions& starts,
                                              std::optional<std::string> selected_path,
                                              const PremadeWorldServices& supplied) {
    if (!selected_path)
        return {};
    const auto services = supplied;
    if (selected_path->find('\0') != std::string::npos)
        throw std::invalid_argument("Premade map path must not contain NUL");
    services.validate();
    if (runtime.has_pending_commands())
        throw std::logic_error("Premade load requires settled session commands");
    return complete(runtime, [&]() -> std::optional<WorldSource> {
        publish(runtime);
        auto input = services.open(*selected_path);
        if (!input) {
            publish(runtime);
            services.load_failure();
            return {};
        }
        bool loaded = false;
        try {
            const formats::ReadItem read = [&](std::size_t count) {
                publish(runtime);
                return input->item(count);
            };
            const world::AllocationCheck allocate = [&](world::StorageRequest request) {
                auto& map = runtime.world().map;
                if (request.kind == world::StorageKind::tiles) {
                    runtime.world().world_setup.width =
                        static_cast<std::int16_t>(map.geometry().width());
                    runtime.world().world_setup.height =
                        static_cast<std::int16_t>(map.geometry().height());
                }
                if (services.allocate) {
                    publish(runtime);
                    const auto revision = map.storage_revision();
                    services.allocate(request);
                    if (map.storage_revision() != revision)
                        throw std::out_of_range("Premade allocation callback replaced map backing");
                }
            };
            loaded = formats::read_map_items(read, formats::MapStream::raw, runtime.world().map,
                                             starts, allocate);
            publish(runtime);
            (void)input->close();
        } catch (...) {
            publish(runtime);
            throw;
        }
        if (!loaded) {
            publish(runtime);
            services.load_failure();
            return {};
        }
        return prepare_premade_world(runtime, starts, services.choices);
    });
}
} // namespace openciv::session
