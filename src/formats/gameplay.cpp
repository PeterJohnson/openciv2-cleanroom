module openciv.formats.gameplay;

import std;
import openciv.base.hash;
import openciv.formats.content_reader;

// Native gameplay-format.md sections 1-7 and the six version-1 component schemas.

namespace openciv::formats::content {
namespace {
constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
using detail::Reader;
state::Rational rational(Reader root) {
    root.fields({"numerator", "denominator"});
    auto n = root.at("numerator").integer(0), d = root.at("denominator").integer(1);
    const auto factor = std::gcd(n, d);
    n /= factor;
    d /= factor;
    root.value["numerator"] = jt::Json(n);
    root.value["denominator"] = jt::Json(d);
    return {n, d};
}
void calendar(Reader root, state::CalendarValues& out) {
    root.fields({"format", "profiles", "retirement"}, true);
    auto profiles = root.at("profiles");
    profiles.fields(state::calendar_profiles);
    for (std::size_t i = 0; i < state::calendar_profiles.size(); ++i) {
        auto profile = profiles.at(state::calendar_profiles[i]);
        profile.fields({"periods"});
        auto periods = profile.at("periods");
        if (!periods.value.isArray() || periods.value.getArray().size() != 6)
            periods.error("expected six ordered periods");
        std::int64_t end = 0;
        for (std::size_t j = 0; j < 6; ++j) {
            auto period = periods.element(j);
            period.fields({"startYear", "turnCount", "yearStep"});
            auto& row = out.profiles[i][j];
            row = {period.at("startYear").integer(), period.at("turnCount").integer(1),
                   period.at("yearStep").integer(1)};
            if (j && row.start_year != end)
                period.error("discontinuous calendar period");
            end = row.start_year + static_cast<std::int64_t>(row.turn_count) * row.year_step;
            if (end < minimum || end > maximum)
                period.error("calendar endpoint outside authoring domain");
        }
    }
    auto retirement = root.at("retirement");
    retirement.fields({"warningYear", "endYear", "scenarioWarningLeadTurns"});
    out.warning_year = retirement.at("warningYear").integer();
    out.end_year = retirement.at("endYear").integer();
    out.scenario_warning_lead_turns = retirement.at("scenarioWarningLeadTurns").integer(0);
    if (out.warning_year >= out.end_year)
        retirement.error("warning must precede retirement year");
}
void spaceship(Reader root, state::SpaceshipValues& out) {
    root.fields({"format", "parts", "peoplePerHabitation"}, true);
    auto parts = root.at("parts");
    parts.fields(state::spaceship_part_names);
    for (std::size_t i = 0; i < out.parts.size(); ++i) {
        auto part = parts.at(state::spaceship_part_names[i]);
        part.fields({"group", "maximumCount", "massTons"});
        const auto group = i == 0 ? "structural" : i < 3 ? "component" : "module";
        part.at("group").constant(group);
        auto& row = out.parts[i];
        row.group = i == 0  ? state::SpaceshipGroup::structural
                    : i < 3 ? state::SpaceshipGroup::component
                            : state::SpaceshipGroup::module;
        row.maximum_count = part.at("maximumCount").integer(0, 32767);
        const auto mass = part.at("massTons").integer(100);
        if (mass % 100)
            part.at("massTons").error("mass must be divisible by 100 tons");
        row.mass_hundreds_of_tons = mass / 100;
    }
    out.people_per_habitation = root.at("peoplePerHabitation").integer(1);
}
void governments(Reader root, std::array<state::GovernmentValues, 7>& out) {
    root.fields({"format", "governments"}, true);
    auto records = root.at("governments");
    records.fields(state::government_names);
    for (std::size_t i = 0; i < out.size(); ++i) {
        auto record = records.at(state::government_names[i]);
        record.fields({"maximumRateTenths", "requiredAdvance", "corruptionDivisor"});
        out[i].maximum_rate_tenths = record.at("maximumRateTenths").integer(0, 10);
        out[i].corruption_divisor = record.at("corruptionDivisor").integer(1);
        auto advance = record.at("requiredAdvance");
        if (i < 2) {
            if (!advance.value.isNull())
                advance.error("this government has no required advance");
        } else
            out[i].required_advance = advance.id();
    }
}
void difficulty(Reader root, std::array<state::DifficultyValues, 6>& out) {
    root.fields({"format", "difficulties"}, true);
    auto records = root.at("difficulties");
    records.fields(state::difficulty_names);
    for (std::size_t i = 0; i < out.size(); ++i) {
        auto record = records.at(state::difficulty_names[i]);
        record.fields({"aiBoxFactor", "humanResearchMultiplier", "aiResearchMultiplier"});
        out[i] = {record.at("aiBoxFactor").integer(1),
                  record.at("humanResearchMultiplier").integer(1),
                  record.at("aiResearchMultiplier").integer(1)};
    }
}
void combat(Reader root, state::CombatValues& out) {
    root.fields({"format", "strengthBonuses", "defenseMultipliers", "antiAirMultipliers",
                 "partisansAgainstZeroAttackMultiplier"},
                true);
    auto bonuses = root.at("strengthBonuses");
    bonuses.fields({"veteran", "paradrop"});
    out.veteran_bonus = rational(bonuses.at("veteran"));
    out.paradrop_bonus = rational(bonuses.at("paradrop"));
    auto defense = root.at("defenseMultipliers");
    defense.fields({"fortification", "fortress", "cityWalls"});
    const auto factor = [&](std::string_view key) {
        auto field = defense.at(key);
        const auto ratio = rational(field);
        const auto doubled = 2 * static_cast<std::int64_t>(ratio.numerator);
        if (doubled % ratio.denominator || doubled / ratio.denominator < 1 ||
            doubled / ratio.denominator > maximum)
            field.error("doubled defense multiplier must be a positive authoring integer");
        return static_cast<std::int32_t>(doubled / ratio.denominator);
    };
    out.fortification_factor = factor("fortification");
    out.fortress_factor = factor("fortress");
    out.city_walls_factor = factor("cityWalls");
    auto air = root.at("antiAirMultipliers");
    air.fields({"ordinaryAircraft", "destroyedAfterAttack"});
    out.ordinary_aircraft_multiplier = air.at("ordinaryAircraft").integer(1);
    out.destroyed_aircraft_multiplier = air.at("destroyedAfterAttack").integer(1);
    out.partisans_against_zero_attack_multiplier =
        root.at("partisansAgainstZeroAttackMultiplier").integer(1);
}
void scoring(Reader root, state::ScoringValues& out) {
    root.fields({"format", "citizens", "wonderPoints", "spaceshipPointsPerPopulationSuccess",
                 "pollutionPointsPerTile", "peace", "futureTechnologyPoints", "barbarianActivity",
                 "conquest", "objectiveValue", "scenario", "rating"},
                true);
    auto citizens = root.at("citizens");
    citizens.fields({"perCitizen", "perHappyCitizen", "perUnhappyCitizen"});
    out.per_citizen = citizens.at("perCitizen").integer();
    out.per_happy_citizen = citizens.at("perHappyCitizen").integer();
    out.per_unhappy_citizen = citizens.at("perUnhappyCitizen").integer();
    out.wonder_points = root.at("wonderPoints").integer();
    out.spaceship_points_per_population_success =
        root.at("spaceshipPointsPerPopulationSuccess").integer();
    out.pollution_points_per_tile = root.at("pollutionPointsPerTile").integer();
    out.future_technology_points = root.at("futureTechnologyPoints").integer();
    auto peace = root.at("peace");
    peace.fields({"firstEligibleTurn", "pointsPerTurn", "maximumPoints"});
    out.peace = {peace.at("firstEligibleTurn").integer(0), peace.at("pointsPerTurn").integer(),
                 peace.at("maximumPoints").integer(0)};
    auto barbarians = root.at("barbarianActivity");
    barbarians.fields({"pointsPerLevel", "offsetPoints"});
    out.barbarians = {barbarians.at("pointsPerLevel").integer(),
                      barbarians.at("offsetPoints").integer()};
    auto conquest = root.at("conquest");
    conquest.fields({"pointsPerEnemy", "basePoints", "turnReference", "pointsPerRemainingTurn",
                     "noRestartMinimumTurn", "largeMapMultiplier", "smallMapMultiplier"});
    out.conquest = {conquest.at("pointsPerEnemy").integer(),
                    conquest.at("basePoints").integer(),
                    conquest.at("turnReference").integer(0),
                    conquest.at("pointsPerRemainingTurn").integer(),
                    conquest.at("noRestartMinimumTurn").integer(0),
                    rational(conquest.at("largeMapMultiplier")),
                    rational(conquest.at("smallMapMultiplier"))};
    auto objective = root.at("objectiveValue");
    objective.fields({"city", "wonder"});
    out.objective_city = objective.at("city").integer(0);
    out.objective_wonder = objective.at("wonder").integer(0);
    auto scenario = root.at("scenario");
    scenario.fields({"outcomePoints", "pointsPerObjective", "conquestMultiplier"});
    auto outcomes = scenario.at("outcomePoints");
    outcomes.fields(state::scenario_outcomes);
    for (std::size_t i = 0; i < out.outcome_points.size(); ++i)
        out.outcome_points[i] = outcomes.at(state::scenario_outcomes[i]).integer();
    out.points_per_objective = scenario.at("pointsPerObjective").integer();
    out.scenario_conquest_multiplier = scenario.at("conquestMultiplier").integer(0);
    auto rating = root.at("rating");
    rating.fields({"difficultyFactors"});
    auto factors = rating.at("difficultyFactors");
    factors.fields(state::difficulty_names);
    for (std::size_t i = 0; i < out.rating_difficulty_factors.size(); ++i)
        out.rating_difficulty_factors[i] = factors.at(state::difficulty_names[i]).integer(1);
}
std::string sha_text(std::string_view text) {
    return base::sha256({reinterpret_cast<const Byte*>(text.data()), text.size()});
}
} // namespace
std::shared_ptr<const GameplayBundle> load_gameplay(std::span<const Byte> manifest,
                                                    const Resolver& resolve, bool require_classic) {
    auto document = parse_json({reinterpret_cast<const char*>(manifest.data()), manifest.size()},
                               "gameplay.json");
    Reader root{document.value, document.numbers, "gameplay.json"};
    root.fields({"format", "id", "revision", "target", "mode", "components", "provenance"}, true);
    root.at("format").constant("openciv2-gameplay/1");
    root.at("target").constant(gameplay_target);
    auto out = std::make_shared<GameplayBundle>();
    out->id = root.at("id").id();
    out->revision = root.at("revision").revision();
    out->manifest_sha256 = base::sha256(manifest);
    const auto mode = root.at("mode").text();
    if (mode != "classic" && mode != "custom")
        root.at("mode").error("expected classic or custom mode");
    out->custom_mode = mode == "custom";
    auto provenance = root.at("provenance");
    provenance.fields({"authors", "license", "notes"});
    provenance.at("authors").texts(true);
    provenance.at("license").text();
    provenance.at("notes").texts(false);
    auto descriptors = root.at("components");
    descriptors.fields(state::gameplay_components);
    std::set<std::string> paths, identities;
    // Validate every descriptor before resolving any external input.
    for (std::size_t i = 0; i < state::gameplay_components.size(); ++i) {
        auto descriptor = descriptors.at(state::gameplay_components[i]);
        descriptor.fields({"path", "sha256"});
        auto& file = out->files[i];
        file.path = descriptor.at("path").text();
        file.sha256 = descriptor.at("sha256").text();
        validate_relative_path(file.path);
        if (!paths.insert(file.path).second)
            descriptor.error("components must use distinct files");
        if (file.sha256.size() != 64 ||
            !std::all_of(file.sha256.begin(), file.sha256.end(),
                         [](char c) { return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'); }))
            descriptor.at("sha256").error("invalid SHA-256");
    }
    jt::Json values;
    values.setObject();
    for (std::size_t i = 0; i < state::gameplay_components.size(); ++i) {
        auto& file = out->files[i];
        Source source;
        try {
            source = resolve(file.path);
        } catch (const std::bad_alloc&) {
            throw;
        } catch (const std::exception& e) {
            throw Error("source-error", file.path, e.what());
        }
        file.identity = source.identity;
        if (file.identity.empty() || !identities.insert(file.identity).second)
            throw Error("source-alias", file.path, "missing or duplicate backing identity");
        if (base::sha256(source.bytes) != file.sha256)
            throw Error("hash-mismatch", file.path, "exact file hash mismatch");
        auto component = parse_json(
            {reinterpret_cast<const char*>(source.bytes.data()), source.bytes.size()}, file.path);
        Reader reader{component.value, component.numbers, file.path};
        reader.at("format").constant("openciv2-" + std::string(state::gameplay_components[i]) +
                                     "/1");
        switch (i) {
        case 0:
            calendar(reader, out->values.calendar);
            break;
        case 1:
            spaceship(reader, out->values.spaceship);
            break;
        case 2:
            governments(reader, out->values.governments);
            break;
        case 3:
            difficulty(reader, out->values.difficulties);
            break;
        case 4:
            combat(reader, out->values.combat);
            break;
        case 5:
            scoring(reader, out->values.scoring);
            break;
        }
        component.value.getObject().erase("extensions");
        values[std::string(state::gameplay_components[i])] = std::move(component.value);
    }
    jt::Json envelope;
    envelope["format"] = jt::Json("openciv2-gameplay-values/1");
    envelope["target"] = jt::Json(std::string(gameplay_target));
    envelope["components"] = std::move(values);
    auto canonical = envelope.toString();
    // jt::Json escapes solidus. The versioned gameplay digest uses the Python
    // ledger's compact ASCII spelling with unescaped '/'. All admitted gameplay
    // strings are ASCII formats, fixed groups or semantic IDs; extensions are gone.
    for (std::size_t pos = 0; (pos = canonical.find("\\/", pos)) != std::string::npos; ++pos)
        canonical.erase(pos, 1);
    out->semantic_sha256 = sha_text(canonical);
    out->classic_values = out->semantic_sha256 == classic_gameplay_sha256;
    if ((!out->custom_mode || require_classic) && !out->classic_values)
        throw Error(
            "classic-values-required", "gameplay.json/mode",
            "changed gameplay operands require custom mode and fail the classic-values gate");
    return out;
}
} // namespace openciv::formats::content
