#pragma once

#include "query/live_records.hpp"
#include "state/records.hpp"

namespace openciv::query {
// Architecture §§4–5 / F02 §3: projections, not authority issuers. The runtime
// supplies the observer after validating its private client lease. Stable slot
// order, occupied records below the live scan bound, and owned values only.
// These specifically query own entities; enemy/remembered views require W03.
LiveRecords<OwnUnit> project_own_units(const state::EntityPools& pools,
                                       LiveObservation observation);
LiveRecords<OwnCity> project_own_cities(const state::EntityPools& pools,
                                        LiveObservation observation);
} // namespace openciv::query
