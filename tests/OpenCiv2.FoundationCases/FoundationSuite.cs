using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Formats;
using OpenCiv2.IO;
using OpenCiv2.Network;
using OpenCiv2.Query;

namespace OpenCiv2.FoundationCases;

// Authored specification/standard vectors, shared by xUnit and the real WASM host.
// This is a bounded foundation suite, not the X02 adapter or a whole-game claim.
public static partial class FoundationSuite
{
    private static readonly (string Name, Action Run)[] Cases =
    [
        ("F01.integer-wrap", IntegerWrap),
        ("F01.signed-division", SignedDivision),
        ("F01.clamps-and-shifts", ClampsAndShifts),
        ("F01.wrapping-expressions", WrappingExpressions),
        ("F01.primary-population", PrimaryPopulation),
        ("F01.secondary-binary64", SecondaryBinary64),
        ("F01.secondary-range", SecondaryRange),
        ("F01.clock-and-map-seed", ClockAndMapSeed),
        ("PCG.reference-vector", PcgReferenceVector),
        ("PCG.context-policy-vector", ContextPolicyVector),
        ("PCG.domain-isolation", DomainIsolation),
        ("PCG.capture-restore", CaptureRestore),
        ("PCG.rejection-preserves-state", RejectionPreservesState),
        ("PCG.constant-decisions", ConstantDecisions),
        ("PCG.culture-independence", CultureIndependence),
        ("F01.controlled-semantic-order", ControlledSemanticOrder),
        ("F01.choice-failure-prefix", ChoiceFailurePrefix),
        ("F01.generated-trace-restore", GeneratedTraceRestore),
        ("F01.choice-checkpoint-admission", ChoiceCheckpointAdmission),
        ("IO.explicit-byte-images", ExplicitByteImages),
        ("IO.truncation-offsets", TruncationOffsets),
        ("IO.close-once-and-short-prefix", CloseOnceAndShortPrefix),
        ("Contracts.descriptor-validation", DescriptorValidation),
        ("Contracts.owned-observations", OwnedObservations),
        ("Base.content-identity-and-names", ContentIdentityAndNames),
        ("Base.poll-only-cancellation", PollOnlyCancellation),
        ("F06.fixed-record-roundtrips", FixedRecordRoundtrips),
        ("F06.fixed-field-offsets", FixedFieldOffsets),
        ("F06.tile-bit-domains", TileBitDomains),
        ("F06.semantic-game-status", SemanticGameStatus),
        ("F06.semantic-text-boundaries", SemanticTextBoundaries),
        ("F06.semantic-map-locations", SemanticMapLocations),
        ("F06.semantic-entity-flags", SemanticEntityFlags),
        ("F06.semantic-memberships", SemanticMemberships),
        ("F06.opaque-option-lifetimes", OpaqueOptionLifetimes),
        ("F06.opaque-flags-live-save", OpaqueFlagsLiveSave),
        ("F06.opaque-event-flags", OpaqueEventFlags),
        ("F06.opaque-tile-lifetimes", OpaqueTileLifetimes),
        ("W01.semantic-tile-mutations", SemanticTileMutations),
        ("F06.stored-stream-order", StoredStreamOrder),
        ("F06.optional-blocks", OptionalBlocks),
        ("F06.event-admission", EventAdmission),
        ("F06.required-block-truncation", RequiredBlockTruncation),
        ("F06.scan-bounds-and-encoding-admission", ScanBoundsAndEncodingAdmission),
        ("F06.legacy-layouts", LegacyLayouts),
        ("Query.owned-records-and-diffs", OwnedRecordsAndDiffs),
        ("Query.semantic-diagnostics", SemanticDiagnostics),
        ("Query.lossless-json-and-raw-maps", LosslessJsonAndRawMaps),
        ("W01.geometry-contract", GeometryContract),
        ("W01.generation-region-labels", GenerationRegionLabels),
        ("W01.generation-border-skips", GenerationRegionBorderSkips),
        ("U03.generation-connectivity-costs", GenerationConnectivityCosts),
        ("W02.land-walks", GenerationLandWalks),
        ("W02.latitude-and-coverage", GenerationLatitudeAndCoverage),
        ("W02.contact-repair", GenerationContactRepair),
        ("W02.alternating-rainfall", GenerationAlternatingRainfall),
        ("W02.weather-and-votes", GenerationWeatherAndVotes),
        ("W02.river-rollback-and-boundary", GenerationRiverRollbackAndBoundary),
        ("W02.fertility-rules", GenerationFertilityRules),
        ("W02.retained-finalization", GenerationRetainedFinalization),
        ("W02.controlled-replay", GenerationControlledReplay),
        ("W02.size-and-parameter-matrix", GenerationSizeAndParameterMatrix),
        ("W02.world-entry-integration", GenerationWorldEntryIntegration),
        ("W02.admission", GenerationAdmission),
        ("F05.start-nearest-queries", StartingNearestQueries),
        ("F05.start-search-relaxation", StartingSearchRelaxation),
        ("F05.start-search-coordinates", StartingSearchCoordinates),
        ("F05.start-search-limits-and-hints", StartingSearchLimitsAndHints),
        ("F05.start-score-contract", StartingScoreContract),
        ("F05.start-search-session-lifetime", StartingSearchSessionLifetime),
        ("F05.start-generated-world-integration", StartingGeneratedWorldIntegration),
        ("C08.building-and-wonder-queries", BuildingAndWonderQueries),
        ("C06.production-snapshot-penalty", ProductionSnapshotAndPenalty),
        ("C06.production-build-forecast", ProductionBuildForecast),
        ("C06.production-human-counters-narrowing", ProductionHumanCountersAndNarrowing),
        ("C06.production-chooser-phase-owner", ProductionChooserPhaseAndOwner),
        ("C06.production-chooser-sentinels-failure", ProductionChooserSentinelsAndFailure),
        ("C06.ai-wonder-start-bonus", ProductionAiWonderStartAndBonus),
        ("C06.ai-wonder-rivals-switch", ProductionAiWonderRivalsAndSwitch),
        ("C06.ai-wonder-abandon", ProductionAiWonderAbandon),
        ("C06.wonder-live-continuation", ProductionWonderLiveContinuation),
        ("C06.production-reset-filters-live-bounds", ProductionResetFiltersAndLiveBounds),
        ("C06.production-workspace-lifetime", ProductionWorkspaceLifetime),
        ("C06.purchase-actual-order-setting", ProductionPurchaseUsesActualOrderSetting),
        ("M4.local-automatic-production", LocalPlayAutomaticProduction),
        ("M4.local-assessment-power", LocalPlayAssessmentAndPower),
        ("A01.assessment-queries", AssessmentQueriesContract),
        ("A01.power-ranks-history-objectives", PowerRanksHistoryAndObjectives),
        ("A01.power-negative-ranks-coordination", PowerNegativeRanksAndCoordination),
        ("A08.government-rates-score-inputs", GovernmentRatesAndScoreInputs),
        ("A08.government-choice-transitions-draws", GovernmentChoiceTransitionsAndDraws),
        ("A01.assessment-ordered-census-aging", AssessmentOrderedCensusAndAging),
        ("A01.assessment-settlers-sleeping", AssessmentSettlersAndSleeping),
        ("A01.assessment-regional-production", AssessmentRegionalPoliciesAndProduction),
        ("A01.assessment-goals-allocation", AssessmentGoalsAndReverseAllocation),
        ("A01.assessment-retirement-upgrade", AssessmentRetirementAndUpgrade),
        ("A01.assessment-elimination-government", AssessmentEliminationAndGovernment),
        ("A01.assessment-paratrooper-staging", AssessmentParatrooperStaging),
        ("A01.assessment-threat-late-region", AssessmentThreatAndLateRegion),
        ("A01.assessment-air-goals-failure-prefix", AssessmentAirGoalsAndFailurePrefix),
        ("A01.assessment-antisubmarine-fortress", AssessmentAntiSubmarineAndFortressGoals),
        ("A01.assessment-historical-route", AssessmentHistoricalRouteRestoresFlags),
        ("A01.assessment-invalid-region-writes-reads", AssessmentInvalidRegionWritesAndReads),
        ("W04.work-command-admission", TerrainWorkCommandAdmission),
        ("W04.work-water-conversion-admission", TerrainWorkWaterAndConversionAdmission),
        ("W04.work-thresholds-progress", TerrainWorkThresholdsAndProgress),
        ("W04.work-donor-stack-completion", TerrainWorkDonorAndStackCompletion),
        ("W04.work-signed-nonsettler", TerrainWorkSignedAndNonsettler),
        ("W04.work-features-knowledge", TerrainWorkFeaturesAndKnowledge),
        ("W04.work-conversion-cleanup-failure", TerrainWorkConversionCleanupAndFailure),
        ("W04.work-fortress-airbase-city", TerrainWorkFortressAirbaseAndCity),
        ("W04.work-refresh-retained-command", TerrainWorkRefreshAndRetainedCommand),
        ("U04.fortification-home-refresh", FortificationHomeAndRefreshOrder),
        ("M4.local-terrain-work-continuation", LocalPlayTerrainWorkContinuation),
        ("A03.terrain-recommendation-gates", AiTerrainRecommendationGates),
        ("A03.city-pollution-goals", CityTerrainPollutionGoals),
        ("A03.city-job-priority-narrowing", CityTerrainPriorityAndNarrowing),
        ("A03.city-railroad-job-retention", CityTerrainRailroadRetention),
        ("A03.road-city-order-treaties", CityRoadPlanningCityOrderAndTreaties),
        ("A03.road-water-rail-endpoint", CityRoadPlanningWaterRailAndEndpoint),
        ("A03.road-first-job-retained-state", CityRoadPlanningFirstJobAndRetainedState),
        ("A03.city-calculation-goals-boundary", CityTerrainCalculationAndBoundary),
        ("M4.local-city-terrain-goals-scores", LocalCityTerrainGoalsAndScores),
        ("M4.local-city-production-events", LocalCityProductionEvents),
        ("M4.local-replay-commands-and-decisions", LocalReplayCommandsAndDecisions),
        ("M4.local-replay-recording-limit", LocalReplayRecordingLimit),
        ("U02.spatial-control-medium-and-city", AiSpatialControlMediumAndCity),
        ("U05.ai-airlift-interception-and-orders", AiAirliftInterceptionAndOrders),
        ("U05.ai-airlift-direction-and-failure-prefix", AiAirliftDirectionAndFailurePrefix),
        ("A02.early-and-barbarian-choices", AiProductionEarlyAndBarbarianChoices),
        ("A02.improvement-ties-and-unrest", AiProductionImprovementTiesAndUnrest),
        ("A02.preferences-and-cutoff", AiProductionPreferencesAndCutoff),
        ("A02.retained-city-and-failure", AiProductionRetainedCityAndFailure),
        ("A02.free-irrigation-visibility", AiProductionFreeIrrigationVisibility),
        ("A02.sales-and-special-recommendations", AiProductionSalesAndSpecialRecommendations),
        ("A02.donor-termination-and-stale-need", AiProductionDonorTerminationAndStaleNeed),
        ("A02.wonder-substitution-and-capacity", AiProductionWonderSubstitutionAndCapacity),
        ("A02.invalid-survey-and-arithmetic", AiProductionInvalidSurveyAndArithmetic),
        ("A02.wonder-raw-choice-retains-weighted-minimum", AiProductionWonderRawChoiceRetainsWeightedMinimum),
        ("A02.diplomat-targets-and-repair", AiProductionDiplomatTargetsAndRepair),
        ("A02.settler-repair-and-signed-roles", AiProductionSettlerRepairAndSignedRoles),
        ("A02.naval-borders-and-threat", AiProductionNavalBordersAndThreat),
        ("A02.defense-override-and-sam", AiProductionDefenseOverrideAndSam),
        ("A02.wonder-expiry-and-rivalry-repair", AiProductionWonderExpiryAndRivalryRepair),


        ("C08.maintenance-quotes", BuildingMaintenanceQuotes),
        ("C08.maintenance-prerequisite-walk", BuildingMaintenancePrerequisiteWalk),
        ("C08.maintenance-ordered-losses", BuildingMaintenanceOrderedLosses),
        ("C08.maintenance-live-continuation", BuildingMaintenanceLiveContinuation),
        ("C08.maintenance-wrap-failure", BuildingMaintenanceWrapAndFailure),
        ("C08.eiffel-acquisition", EiffelAcquisitionEffects),
        ("C08.leonardo-selection-retention", LeonardoSelectionAndRetention),
        ("C08.leonardo-defensive-same-type", LeonardoDefensiveAndSameType),
        ("C08.leonardo-live-continuation-failure", LeonardoLiveContinuationAndFailure),
        ("V04.attitude-primitives", DiplomaticAttitudePrimitives),
        ("V04.global-reputation-maintenance", GlobalReputationMaintenance),
        ("W03.apollo-knowledge-publication", ApolloKnowledgePublication),
        ("V03.research-queries-and-walks", ResearchQueriesAndWalks),
        ("V03.research-selection-resume-and-advice", ResearchSelectionResumeAndAdvice),
        ("V03.research-selection-gates-and-empty-options", ResearchSelectionGatesAndEmptyOptions),
        ("V03.research-advice-ordering-and-future-labels", ResearchAdviceOrderingAndFutureLabels),
        ("V03.research-goal-options-paths-and-replies", ResearchGoalOptionsPathsAndReplies),
        ("V03.research-completion-modes-and-ability-order", ResearchCompletionModesAndAbilityOrder),
        ("V03.research-accumulation-philosophy-and-free-advance", ResearchAccumulationPhilosophyAndFreeAdvance),
        ("V03.research-accumulation-widths-and-initial-choice", ResearchAccumulationWidthsAndInitialChoice),
        ("V03.research-completion-ai-production-and-no-target", ResearchCompletionAiProductionAndNoTarget),
        ("V03.completion-government-failure", ResearchCompletionGovernmentAndFailure),
        ("C06.city-production-dispatch-stocks-and-duplicates", CityProductionDispatchStocksAndDuplicates),
        ("C06.city-production-palace-and-ai-remainder", CityProductionPalaceAndAiRemainder),
        ("C06.city-production-wonder-race-and-loss", CityProductionWonderRaceAndLoss),
        ("C06.city-production-notice-order-and-failure", CityProductionNoticeOrderAndFailure),
        ("C06.city-production-manhattan-and-wonder-children", CityProductionManhattanAndWonderChildren),
        ("C06.city-production-darwin-sequential-research", CityProductionDarwinSequentialResearch),
        ("C06.city-production-spaceship-and-automation", CityProductionSpaceshipAndAutomation),
        ("C06.city-production-ai-spaceship-retarget", CityProductionAiSpaceshipRetarget),
        ("C06.player-purchase-prices-and-completion", PlayerPurchasePricesAndCompletion),
        ("C06.player-purchase-denial-cancellation-and-failure", PlayerPurchaseDenialCancellationAndFailure),
        ("C06.player-purchase-signed-costs-and-limits", PlayerPurchaseSignedCostsAndLimits),
        ("C06.player-sale-restrictions-and-commit", PlayerSaleRestrictionsAndCommit),
        ("C06.building-eligibility-knowledge-and-coasts", BuildingEligibilityKnowledgeAndCoasts),
        ("C06.building-eligibility-facility-chains-and-effects", BuildingEligibilityFacilityChainsAndEffects),
        ("C06.building-eligibility-power-replacement", BuildingEligibilityPowerReplacement),
        ("C06.building-eligibility-spaceships-and-scenario-wonders", BuildingEligibilitySpaceshipsAndScenarioWonders),
        ("C06.production-options-ordered-snapshot-and-expiration", ProductionOptionsOrderedSnapshotAndExpiration),
        ("C06.player-production-change-retains-snapshot", PlayerProductionChangeRetainsSnapshot),
        ("C06.player-production-change-suppression-and-direct-ai", PlayerProductionChangeSuppressionAndDirectAi),
        ("V03.full-research-cost-vectors", ResearchFullCostVectors),
        ("V03.scenario-cost-bounds", ResearchScenarioCostAndBounds),
        ("V03.selection-option-rules", ResearchSelectionOptionRules),
        ("V03.grant-turn-zero-scalar-prefixes", ResearchGrantTurnZeroAndScalarPrefixes),
        ("V03.grant-duplicate-future-first-marker", ResearchGrantDuplicateFutureAndFirstMarker),
        ("V03.grant-announcements-prompts", ResearchGrantAnnouncementsAndPrompts),
        ("V03.grant-emissary-draw-reset", ResearchGrantEmissaryDrawAndReset),
        ("V03.grant-barracks-failure", ResearchGrantBarracksAndFailure),
        ("V03.grant-philosophy-wonder-expiry", ResearchGrantPhilosophyAndWonderExpiry),
        ("V03.golden-age-weighted-selection", ResearchGoldenAgeWeightedSelection),
        ("V03.grant-leonardo-great-library", ResearchGrantLeonardoAndGreatLibrary),
        ("V03.grant-failure-prefixes", ResearchGrantFailurePrefixes),
        ("V03.government-prompt-transitions", ResearchGovernmentPromptTransitions),
        ("V03.government-tutorial-return-gates", ResearchGovernmentTutorialAndReturnGates),
        ("A08.research-valuation-state", ResearchValuationState),
        ("A08.research-valuation-expansion-and-race", ResearchValuationExpansionAndRace),
        ("A08.research-valuation-tribes-and-knowledge", ResearchValuationTribesAndKnowledge),
        ("V03.recommendation-and-starting-grant", ResearchRecommendationAndStartingGrant),
        ("U02.unit-movement-allowances", UnitMovementAllowances),
        ("U04.activation-predicates", UnitActivationPredicates),
        ("U04.activation-waiting-and-ties", UnitActivationWaitingAndTies),
        ("W03.knowledge-channels", VisibilityKnowledgeChannels),
        ("W03.tile-and-stack-publication", VisibilityTileAndStack),
        ("U04.shared-contact-orders", VisibilitySharedOrders),
        ("W03.surroundings-footprint", VisibilityFootprint),
        ("W03.adjacent-cities", VisibilityAdjacentCities),
        ("W03.adjacent-detection", VisibilityAdjacentDetection),
        ("W03.contact-continuation", VisibilityContactContinuation),
        ("W03.outer-observers", VisibilityOuterObservers),
        ("U01.creation-placement-and-home", UnitCreationPlacementAndHome),
        ("U01.creation-retained-damage-and-wrap", UnitCreationRetainedDamageAndWrap),
        ("U01.creation-capacity-and-failures", UnitCreationCapacityAndFailures),
        ("U01.creation-notices", UnitCreationNotices),
        ("V01.creation-reset", CivilizationCreationReset),
        ("V01.creation-early-returns-and-leaders", CivilizationCreationEarlyReturnsAndLeaders),
        ("V01.creation-catch-up", CivilizationCreationCatchup),
        ("V01.restart-research-library", RestartResearchLibraryAndUpgrade),
        ("V01.restart-research-government-failure", RestartResearchGovernmentAndFailurePrefix),
        ("V01.elimination-restart-research", EliminationRestartActualResearch),
        ("V01.creation-hints-and-rejection", CivilizationCreationHintsAndRejection),
        ("V01.creation-search-rescue", CivilizationCreationSearchRescue),
        ("V01.creation-revival-placement", CivilizationCreationRevivalPlacement),
        ("V01.creation-admission-and-failure", CivilizationCreationAdmissionAndFailure),
        ("C06.unit-build-eligibility", UnitBuildEligibility),
        ("C07.commodity-published-vector", CommodityPublishedVector),
        ("C07.commodity-survey-road-repair", CommoditySurveyAndRoadRepair),
        ("C07.commodity-technology-signed-scores", CommodityTechnologyAndSignedScores),
        ("C07.commodity-facilities-tribes", CommodityFacilitiesAndTribes),
        ("C07.commodity-gates-ranking", CommodityGatesAndRanking),
        ("C07.commodity-exceptional-suppression", CommodityExceptionalAndSuppression),
        ("C01.creation-initialization", CityCreationInitialization),
        ("C01.creation-production-failures", CityCreationProductionAndFailures),
        ("C01.creation-limits-wrap", CityCreationLimitsAndWrap),
        ("C01.creation-ai-first-city", CityCreationAiFirstCity),
        ("C01.creation-coast-area", CityCreationCoastAndArea),
        ("C01.region-port-queries", CityRegionAndPortQueries),
        ("C01.coastal-queries", CityCoastalQueries),
        ("F05.starting-compensation", StartingCompensation),
        ("F05.generated-civilization-integration", GeneratedCivilizationIntegration),
        ("F05.generated-initialization-lifetime", GeneratedInitializationLifetime),
        ("F05.world-entry-civilization-creation", GeneratedWorldEntryCreation),
        ("F05.accelerated-early-package", AcceleratedEarlyPackage),
        ("F05.accelerated-advanced-packages", AcceleratedAdvancedPackages),
        ("F05.accelerated-identities-research", AcceleratedIdentityAndResearch),
        ("F05.accelerated-last-unit-retention", AcceleratedLastUnitAndRetention),
        ("F05.accelerated-tile-development", AcceleratedTileDevelopment),
        ("F05.accelerated-roads-peace", AcceleratedRoadsAndPeace),
        ("F05.accelerated-failure-prefixes", AcceleratedFailurePrefixes),
        ("V03.starting-grant-city-domains", StartingGrantCityDomains),
        ("F05.generated-acceleration-integration", GeneratedAccelerationIntegration),
        ("F05.generated-acceleration-lifetime", GeneratedAccelerationLifetime),
        ("F05.world-entry-acceleration", GeneratedWorldEntryAcceleration),
        ("C02.tile-yield-modifiers", CityYieldModifiers),
        ("C02.tile-resources-signed-rules", CityYieldResourcesAndSignedRules),
        ("C02.availability-pollution-visits", CityAvailabilityAndPollutionVisits),
        ("C02.terrain-suggestions-water", CityTerrainSuggestionsAndWater),
        ("C02.worker-ties-retention", CityWorkerTiesAndRetention),
        ("C02.worker-greedy-stages", CityWorkerGreedyStages),
        ("C02.worker-ai-cadence-support", CityWorkerAiCadenceAndSupport),
        ("C02.specialist-domains", CityWorkerSpecialistDomains),
        ("C02.failure-isolation", CityWorkingFailureAndIsolation),
        ("C05.box-factors", CitySupportBoxFactors),
        ("C05.support-charge-domains", CitySupportChargeAndDomains),
        ("C05.military-membership", CitySupportMilitaryMembership),
        ("C05.growth-facility-order", CityGrowthFacilityOrder),
        ("C03.loss-examples", CityLossPublishedExamples),
        ("C03.production-pollution-waste", CityIndustrialPollutionWaste),
        ("C03.capital-raw-overlap", CityCapitalAndRawOverlap),
        ("U03.road-connection-semantics", CityRoadConnectionSemantics),
        ("C03.food-route-multiplicity", CityFoodRouteMultiplicity),
        ("C03.distribution-rates-specialists", CityDistributionRatesAndSpecialists),
        ("C03.distribution-facilities-wonders", CityDistributionFacilitiesAndWonders),
        ("C03.capitalization-publication", CityCapitalizationAndScalarPublication),
        ("C03.recurring-trade-snapshots", CityRecurringTradeSnapshots),
        ("C04.mood-normalization", CityMoodNormalizationExamples),
        ("C04.mood-stages-military", CityMoodStagesAndMilitary),
        ("C03.full-calculation-cache", CityFullCalculationAndCachedState),
        ("C03.failure-prefixes", CityEconomyFailurePrefixes),
        ("C03.generated-calculation-integration", GeneratedCityCalculationIntegration),
        ("U01.ships-first-staging", CargoStableShipsAndStaging),
        ("U01.cargo-human-priority", CargoHumanPriorityAndSleep),
        ("U01.cargo-ai-regional-policy", CargoAiRegionalEligibility),
        ("U01.cargo-aircraft-signed-capacity", CargoAircraftAndSignedCapacity),
        ("U01.cargo-lift-repeat-queries", CargoLiftAndRepeatedQueries),
        ("U01.cargo-loss-sea-land-staging", CargoLossSeaLandAndStaging),
        ("U01.cargo-failure-authority", CargoFailureAndAuthority),
        ("C07.route-remove-write", RouteRemovalAndWriting),
        ("C07.route-append-duplicates", RouteAppendDuplicatesAndRefresh),
        ("C07.route-replacement-repair", RouteReplacementValuationAndRepair),
        ("C07.route-failure-prefixes", RouteMutationFailurePrefixes),
        ("C05.retirement-foreign-restart", RetirementForeignRestartAndTies),
        ("C05.retirement-free-cargo", RetirementFreeUnitsAndCargo),
        ("C05.retirement-affordable-policy", RetirementAffordableAndCoordination),
        ("C05.retirement-protection-domains", RetirementProtectionAndHomeDomains),
        ("C05.retirement-failure-prefixes", RetirementFailurePrefixes),
        ("C01.removal-supported-garrisons", CityRemovalSupportedGarrisons),
        ("C01.removal-routes-wonders", CityRemovalRoutesWondersAndPayload),
        ("C01.removal-spatial-visibility", CityRemovalSpatialVisibility),
        ("C01.removal-raw-homes-guards", CityRemovalRawHomesAndGuards),
        ("C01.removal-failure-prefixes", CityRemovalFailurePrefixes),
        ("C05.food-growth-blocked-refill", FoodGrowthAndBlockedRefill),
        ("C05.food-ai-factors-production", FoodAiFactorsAndProductionBoundary),
        ("C05.food-wonders-scalar-wrap", FoodWondersAndScalarWrap),
        ("C05.famine-selection-visibility", FoodFamineSelectionAndVisibility),
        ("C05.famine-cargo-raw-home", FoodFamineCargoAndRawHome),
        ("C05.famine-routes-workspace", FoodFamineRoutesAndIncomingWorkspace),
        ("C05.famine-city-removal", FoodCityRemovalAndContinuation),
        ("C05.food-accumulation-warning", FoodAccumulationAndWarnings),
        ("C05.food-recalculation-boundaries", FoodRecalculationAndFailureBoundaries),
        ("V06.incident-directed-guards", IncidentDirectedAdmissionAndGuards),
        ("V06.incident-human-pretext", IncidentHumanPretextAndNoExpulsion),
        ("V06.incident-war-senate", IncidentWarAndActualSenateTransition),
        ("V06.incident-scenario-autoplay", IncidentScenarioAndAutoplayOrdering),
        ("V06.incident-leniency-alliance", IncidentLeniencyLiveWorkspaceAndAlliance),
        ("V06.escape-incident-continuation", EscapeIncidentContinuationAndLossPrefix),
        ("V06.incident-failure-prefix", IncidentFailuresRetainWarPrefix),
        ("V06.escape-odds-promotion", SpyEscapeOddsAndPromotion),
        ("V06.escape-relocation-gates", SpyEscapeRelocationAndGates),
        ("V06.capture-ordinary-failure", SpyCaptureOrdinaryAndFailure),
        ("V01.elimination-last-human-recursion", EliminationLastHumanAndRecursion),
        ("V01.elimination-history-spaceship", EliminationHistoryAndShipLoss),
        ("V01.elimination-survivor-spy", EliminationSurvivorAndSpyMission),
        ("V01.elimination-restart-exploration", EliminationRealRestartAndExploration),
        ("V01.elimination-hotseat-retention", EliminationHotseatAndHumanRetention),
        ("C05.famine-elimination-integration", FamineActualEliminationIntegration),
        ("V02.government-queries-identities", GovernmentQueriesAndFixedIdentities),
        ("V02.government-recalculation-production", GovernmentDirectRecalculationAndProduction),
        ("V02.government-arguments-decisions", GovernmentFullArgumentsAndDecisionBoundaries),
        ("V02.government-ai-announcements", GovernmentAiAnnouncementGates),
        ("V02.government-turn-calendar", GovernmentTurnCalendarAndChildren),
        ("F06.hall-records", HallRecordEncodingAndPreparation),
        ("P07.power-history-report", RetainedPowerHistory),
        ("F04.spaceship-arrival-boundary", SpaceshipArrivalBoundary),
        ("V04.declaration-reputation-direction", DeclarationReputationAndDirection),
        ("V04.alliance-land-expulsion", AllianceLandExpulsion),
        ("V04.alliance-ocean-cargo-expulsion", AllianceOceanCargoExpulsion),
        ("V04.alliance-cancellation-declaration", AllianceCancellationAndDeclaration),
        ("V04.war-alliance-obligations", WarAllianceObligations),
        ("V04.forced-joined-war", ForcedWarAndJoinedWar),
        ("V04.war-failure-prefixes", WarFailurePrefixes),
        ("V04.border-first-stack-retention", BorderFirstStackAndRetainedWarnings),
        ("V04.border-stack-ownership-crowding", BorderStackOwnershipAndCrowding),
        ("V04.border-topology-skips", BorderTopologyAndSkips),
        ("A08.stance-regions-retention", StanceRegionalPressureAndRetention),
        ("A08.stance-nuclear-wonders-nearby", StanceNuclearWonderAndNearbyUnits),
        ("A08.stance-third-parties-direction", StanceThirdPartiesAndDirection),
        ("A08.stance-race-scenario", StanceRaceAndScenarioOverrides),
        ("A08.stance-government-patience", StanceGovernmentAndPatience),
        ("A08.alliance-willingness-overrides", AllianceWillingnessOrderedOverrides),
        ("A08.alliance-common-target", AllianceCommonTargetSelection),
        ("V08.scheduled-retirement", ScheduledRetirementEqualities),
        ("V08.pending-outcome-continuation", PendingOutcomeContinuations),
        ("V08.pending-outcome-presentation", PendingOutcomePresentations),
        ("M4.local-scheduled-retirement", LocalScheduledRetirement),
        ("M4.local-famine-defeat", LocalFamineDefeat),
        ("V08.end-game-report-order", EndGameReportOrder),
        ("V08.explicit-retirement", ExplicitRetirementFailureAndFlags),
        ("M4.local-retirement-quit", LocalRetirementAndQuit),
        ("V08.hall-file-transfers", HallSparseReadsAndWrites),
        ("V08.hall-ranking-display", HallRankingAndDisplay),
        ("V08.hall-insertion-continuation", HallInsertionContinuation),
        ("M4.local-hall-viewer", LocalHallViewer),
        ("V08.score-components", ScoreComponentsAndRawWonders),
        ("V08.spaceship-conquest-score", ScoreSpaceshipAndConquest),
        ("V08.objective-score", ObjectiveScoringAndPerspective),
        ("V08.rating-wrap", ScoreRatingAndWrappedValues),
        ("V08.report-preparation", ScoreReportPreparation),
        ("M4.local-score-decisions", LocalScoreDecisions),
        ("V02.treasury-population-queries", TreasuryAndPopulationQueries),
        ("V02.population-funds-notices", PopulationMilestonesAndFunds),
        ("M4.finance-rate-continuation", FinanceRateContinuation),
        ("M4.local-population-turn", LocalPopulationTurn),
        ("V04.human-diplomacy-turn", DiplomacyHumanTurnDrawsAndFlags),
        ("V04.ai-diplomacy-aging", DiplomacyAiAgingAndPolicyOrder),
        ("V04.stale-diplomacy-children", DiplomacyStaleContactChildren),
        ("M4.local-government-turns", LocalGovernmentRevolutionAndTurns),
        ("M4.local-government-failure", LocalGovernmentCancellationAndOutputFailure),
        ("V02.rate-normalization-balancing", RateNormalizationAndBalancing),
        ("V02.rate-cap-decisions", RateCapDecisionOrder),
        ("V02.rate-preview-distribution", RatePreviewRealDistribution),
        ("V02.rate-acceptance-persistence", RateAcceptanceAndPersistence),
        ("M4.local-rates-persistence", LocalPlayRatesAndPersistence),
        ("M4.local-rate-editor-decisions", LocalRateEditorDecisions),
        ("C04.disorder-duration-restoration", CityDisorderDurationAndRestoration),
        ("C04.disorder-democracy-collapse", CityDisorderDemocracyCollapse),
        ("C04.mood-tax-wrap-gates", CityMoodTaxWrapAndPlayerGates),
        ("C04.celebration-growth-facilities", CityCelebrationGrowthAndFacilityGates),
        ("C04.celebration-qualification-notices", CityCelebrationQualificationAndNotices),
        ("C06.unit-stock-capacity", UnitProductionStocksAndCapacity),
        ("C06.unit-disorder-barbarian", UnitProductionDisorderAndBarbarianStocks),
        ("C06.unit-first-upgrade", UnitProductionFirstUpgradeAndNoChain),
        ("C06.unit-veterans-transport", UnitProductionVeteransAndTransport),
        ("C06.unit-settler-refusal-population", UnitProductionSettlerRefusalAndPopulation),
        ("C06.unit-settler-cancel-removal", UnitProductionSettlerCancellationAndRemoval),
        ("C06.unit-ai-diplomat-nuclear", UnitProductionAiDiplomatsAndNuclearContact),
        ("C06.unit-policy-failure-prefixes", UnitProductionPolicyContinuationsAndFailures),
        ("V07.part-capacity-thrust", SpaceshipPartCapacityAndThrust),
        ("V07.group-raw-supported", SpaceshipGroupRawAndSupportedCounts),
        ("V07.race-arrival-predicates", SpaceshipRaceAndArrivalPredicates),
        ("F04.human-wonder-summary", HumanWonderSummaryRetention),
        ("C06.purchase-policy-reselection", PurchaseRegionalPolicyAndReselection),
        ("C06.purchase-wonder-thresholds", PurchaseWonderGroupsAndThresholds),
        ("C06.purchase-ordered-overrides", PurchaseOrderedOverridesAndNuclearIdentity),
        ("C06.purchase-spaceship-threat", PurchaseSpaceshipThreatInputs),
        ("C06.purchase-refund-failure", PurchaseRefundArithmeticAndFailure),
        ("C06.purchase-almost-wonder", PurchaseAlmostWonderTiming),
        ("C06.unit-actual-purchase", UnitProductionActualPurchaseContinuation),
        ("V07.performance-classic-fusion", SpaceshipPerformanceClassicAndFusion),
        ("V07.performance-missing-unsupported", SpaceshipPerformanceMissingAndUnsupported),
        ("V07.preview-retention-calendar", SpaceshipPreviewRetentionAndCalendar),
        ("V07.configured-maxima-mass", SpaceshipConfiguredMaximaAndMass),
        ("V07.group-choice-race", SpaceshipGroupChoiceAndRace),
        ("V07.group-emergency-equality", SpaceshipGroupEmergencyEquality),
        ("V07.human-selection-repeats", SpaceshipHumanSelectionAndRepeats),
        ("V07.first-ship-ai-selection", SpaceshipFirstShipContactsAndAiChoices),
        ("V07.ai-launch-thresholds", SpaceshipAiLaunchThresholds),
        ("V07.ai-launch-race-slot-zero", SpaceshipAiLaunchRaceAndSlotZero),
        ("V07.launch-mutations-callbacks", SpaceshipLaunchMutationsAndCallbacks),
        ("V07.human-launch-relations", SpaceshipHumanLaunchRelations),
        ("V04.relation-cascades", DiplomaticRelationCascades),
        ("W03.map-observer-privacy", MapObserverPrivacy),
        ("P05.map-target-command", MapTargetCommand),
        ("M3.local-play-round-trip", LocalPlayRoundTrip),
        ("M3.land-step-zones", LocalStepZones),
        ("M3.local-play-specialist-admission", LocalPlaySpecialistAdmission),
        ("M3.local-support-retirement", LocalPlaySupportRetirement),
        ("M3.local-food-turn", LocalPlayFoodTurn),
        ("M3.local-mood-turn", LocalPlayMoodTurn),
        ("M4.local-maintenance-reputation-turn", LocalPlayMaintenanceAndReputation),
        ("M4.local-nonunit-production", LocalPlayNonunitProduction),
        ("M4.local-buying-and-selling", LocalPlayBuyingAndSelling),
        ("M4.local-research-grant-turn", LocalPlayResearchGrant),
        ("M3.local-production-stocks", LocalPlayProductionStocks),
        ("M3.local-play-exhausted-step", LocalPlayExhaustedStep),
        ("P03.atlas-admission", AtlasAdmission),
        ("P03.png-filters-and-failures", PngFiltersAndFailures),
        ("P03.pinned-package-admission", PinnedPackageAdmission),
        ("Content.strict-json-admission", StrictJsonAdmission),
        ("Content.exact-integers", ExactContentIntegers),
        ("Content.trusted-gameplay-values", GameplayTrustedValues),
        ("Content.canonical-normalization", GameplayCanonicalNormalization),
        ("Content.custom-gameplay-gate", GameplayCustomGate),
        ("Content.descriptor-admission", GameplayDescriptorAdmission),
        ("Content.invalid-documents", GameplayInvalidDocuments),
        ("Pack.owned-receipts", PackOwnedReceipts),
        ("Pack.overlay-order", PackOverlayOrder),
        ("Pack.parent-admission", PackParentAdmission),
        ("Pack.declaration-admission", PackDeclarationAdmission),
        ("Pack.locale-admission", PackLocaleAdmission),
        ("Pack.owned-media-and-hashes", PackOwnedMediaAndHashes),
        ("Pack.nested-gameplay", PackNestedGameplay),
        ("Pack.budgets-and-revisions", PackBudgetsAndRevisions),
        ("F03.classic-text-exact", ClassicTextExact),
        ("F03.rule-identity-order", RulesIdentityOrder),
        ("F03.numeric-normalization", RulesNumericNormalization),
        ("F03.advance-fixup", RulesAdvanceFixup),
        ("F03.retained-installation", RulesRetainedInstallation),
        ("F03.reload-dependencies-and-prefix", RulesReloadDependenciesAndFailurePrefix),
        ("F02.text-and-label-state", RuleTextAndLabelState),
        ("F03.rule-rejection", RulesRejection),
        ("F03.city-preference-updates", CityPreferenceUpdates),
        ("F03.city-preference-admission", CityPreferenceAdmission),
        ("C01.name-catalog-selection", CityNameCatalogSelection),
        ("C01.name-layer-ownership", CityNameLayerOwnership),
        ("C01.name-admission", CityNameAdmission),
        ("F03.classic-read-windows", ClassicReadWindows),
        ("F03.classic-tokens-and-numbers", ClassicTokensAndNumbers),
        ("F03.classic-read-failure-state", ClassicReadFailureState),
        ("F03.classic-source-fallback", ClassicSourceFallback),
        ("F03.classic-section-search", ClassicSectionSearch),
        ("F03.classic-indexed-search", ClassicIndexedSearch),
        ("F03.classic-service-admission", ClassicServiceAdmission),
        ("F03.classic-case-and-request-ownership", ClassicCaseAndRequestOwnership),
        ("F03.classic-rules-complete", ClassicRulesComplete),
        ("F03.classic-rule-operands-and-reload", ClassicRuleOperandsAndReload),
        ("F03.classic-rule-vocabulary-and-defaults", ClassicRuleVocabularyAndDefaults),
        ("F03.classic-rule-retained-names", ClassicRuleRetainedNames),
        ("F03.classic-rule-rejection", ClassicRuleRejection),
        ("F03.classic-rule-text-and-titles", ClassicRuleTextAndTitles),
        ("F03.classic-rule-selection-failures", ClassicRuleSelectionFailures),
        ("F03.classic-preference-decoding", ClassicPreferenceDecoding),
        ("F03.classic-preference-snapshot-and-cleanup", ClassicPreferenceSnapshotAndCleanup),
        ("C01.classic-name-layers", ClassicNameLayers),
        ("C01.classic-name-prefixes-and-cleanup", ClassicNamePrefixesAndCleanup),
        ("Conversion.rules-complete", ConversionRulesComplete),
        ("Conversion.rules-reload", ConversionRulesReload),
        ("Conversion.rules-rejection", ConversionRulesRejection),
        ("Conversion.names-cursors", ConversionNamesCursors),
        ("Conversion.names-admission", ConversionNamesAdmission),
        ("Conversion.names-windows", ConversionNamesWindows),
        ("Conversion.preference-effects", ConversionPreferenceEffects),
        ("Conversion.preference-limits", ConversionPreferenceLimits),
        ("Conversion.preference-text", ConversionPreferenceText),
        ("Conversion.preference-rejection", ConversionPreferenceRejection),
        ("F02.entity-token-sequences", EntityTokenSequences),
        ("F02.entity-reservation-first-hole", EntityReservationFirstHole),
        ("F02.entity-reservation-thresholds", EntityReservationThresholds),
        ("F02.entity-reservation-admission", EntityReservationAdmission),
        ("F02.entity-reservation-retained-scan", EntityReservationRetainedScan),
        ("F02.entity-pool-deletion", EntityPoolDeletion),
        ("F02.entity-pool-reservation", EntityPoolReservation),
        ("F02.entity-pool-resets", EntityPoolResets),
        ("F02.entity-pool-renewal", EntityPoolRenewal),
        ("F02.entity-record-copies", EntityRecordCopies),
        ("F02.entity-mood-dispatch", EntityMoodDispatch),
        ("W01.map-allocation-and-release", MapAllocationAndRelease),
        ("W01.map-replacement-and-swap", MapReplacementAndSwap),
        ("W01.map-direct-layer-indices", MapDirectLayerIndices),
        ("W01.map-retained-spatial-reads", MapRetainedSpatialReads),
        ("W01.map-coordinate-patterns", MapCoordinatePatterns),
        ("W01.map-mutation-fields", MapMutationFields),
        ("W01.map-synchronization", MapSynchronization),
        ("F06.civilization-census-counts", CivilizationCensusCounts),
        ("F06.civilization-census-failures", CivilizationCensusFailures),
        ("F06.civilization-value-copies", CivilizationValueCopies),
        ("F06.unit-live-conversion", UnitLiveConversion),
        ("F06.city-live-conversion", CityLiveConversion),
        ("F06.civilization-live-conversion", CivilizationLiveConversion),
        ("F06.globals-live-conversion", GlobalsLiveConversion),
        ("S02.event-presence", EventPresence),
        ("S02.event-copy-aliases", EventCopyAliases),
        ("S02.event-copy-nonempty", EventCopyNonempty),
        ("S02.event-compact-identity", EventCompactIdentity),
        ("S02.event-installation", EventInstallation),
        ("F02.world-ownership", WorldOwnership),
        ("F02.world-value-copies", WorldValueCopies),
        ("F03.preference-defaults", PreferenceDefaults),
        ("F03.configuration-cancellation-admission", ConfigurationCancellationAndAdmission),
        ("F03.configuration-game-bits", ConfigurationGameBits),
        ("F03.configuration-music-transitions", ConfigurationMusicTransitions),
        ("F03.configuration-failure-prefixes", ConfigurationFailurePrefixes),
        ("F03.configuration-graphics-warning", ConfigurationGraphicsBitsAndWarning),
        ("F03.configuration-message-replacement", ConfigurationMessageReplacement),
        ("F03.configuration-world-pin-publication", ConfigurationWorldPinAndPublication),
        ("F03.double-movement-byte-laws", DoubleMovementByteLaws),
        ("F03.double-movement-gates-failure", DoubleMovementGatesAndFailure),
        ("N03.options-admission", MultiplayerOptionAdmission),
        ("N03.options-yield-apply", MultiplayerOptionYieldAndApply),
        ("N03.options-local-gates", MultiplayerOptionLocalGates),
        ("N03.options-rejection-deadlines", MultiplayerOptionRejectionAndDeadlines),
        ("N03.options-handoff-live-effects", MultiplayerOptionHandoffAndLiveEffects),
        ("N03.options-cancellation", MultiplayerOptionCancellation),
        ("N03.options-child-failures", MultiplayerOptionChildFailures),
        ("F06.password-rotation", PasswordRotation),
        ("F06.password-reset", PasswordReset),
        ("F06.password-reset-failure", PasswordResetFailure),
        ("F06.password-refresh", PasswordRefresh),
        ("F06.password-replay", PasswordReplay),
        ("F06.multiplayer-mode-adaptation", MultiplayerModeAdaptation),
        ("F06.names-live-conversion", NamesLiveConversion),
        ("F06.scenario-history-live-conversion", ScenarioHistoryLiveConversion),
        ("F06.multiplayer-live-conversion", MultiplayerLiveConversion),
        ("F06.map-view-live-conversion", MapViewLiveConversion),
        ("F06.event-values-live-conversion", EventValuesLiveConversion),
        ("A01.goal-duplicate-admission", GoalDuplicateAdmission),
        ("A01.goal-priority-insertion", GoalPriorityInsertion),
        ("A01.goal-gap-rules", GoalGapRules),
        ("A01.goal-full-lists", GoalFullLists),
        ("A01.goal-negation-and-query", GoalNegationAndQuery),
        ("A01.goal-refresh-ordering", GoalRefreshOrdering),
        ("A01.goal-standing-distance", GoalStandingDistance),
        ("A01.goal-redirection-gates", GoalRedirectionGates),
        ("A01.goal-redirection-capacity", GoalRedirectionCapacity),
        ("A01.goal-redirection-failures", GoalRedirectionFailures),
        ("A01.goal-backing-and-wrapping", GoalBackingAndWrapping),
        ("A01.goal-record-retention", GoalRecordRetention),
        ("F04.calendar-profiles", CalendarProfiles),
        ("F04.calendar-periods", CalendarPeriods),
        ("F04.calendar-signed-turns", CalendarSignedTurns),
        ("F04.calendar-launch", CalendarLaunch),
        ("F04.calendar-scenario", CalendarScenario),
        ("F04.calendar-custom", CalendarCustom),
        ("F04.calendar-observation", CalendarObservation),
        ("V03.knowledge-sentinels", KnowledgeSentinels),
        ("C01.city-naming-cursors", CityNamingCursors),
        ("C01.city-naming-missing-and-retry", CityNamingMissingAndRetry),
        ("C01.city-naming-text-and-failure", CityNamingTextAndFailure),
        ("C01.city-naming-live-references", CityNamingLiveReferences),
        ("C01.city-naming-installed-rules", CityNamingInstalledRules),
        ("U01.stack-traversal", StackTraversal),
        ("U01.stack-non-repairs", StackNonRepairs),
        ("U01.stack-dead-passes", StackDeadPasses),
        ("U01.stack-position-cuts", StackPositionCuts),
        ("U01.stack-singleton-detach", StackSingletonDetach),
        ("U01.stack-middle-placement", StackMiddlePlacement),
        ("U01.stack-coordinate-width", StackCoordinateWidth),
        ("U01.stack-owner-width", StackOwnerWidth),
        ("U01.stack-self-loop", StackSelfLoop),
        ("U01.stack-full-repair", StackFullRepair),
        ("U01.stack-execution-stops", StackExecutionStops),
        ("U01.stack-backing-failures", StackBackingFailures),
        ("U01.stack-passive-diagnostics", StackPassiveDiagnostics),
        ("U01.stack-animation-lookup", StackAnimationLookup),
        ("U01.stack-placement-synchronization", StackPlacementSynchronization),
        ("U01.stack-bottom-guards", StackBottomGuards),
        ("U01.stack-whole-relocation", StackWholeRelocation),
        ("U01.stack-authority-and-failure", StackAuthorityAndFailure),
        ("U01.stack-repair-authority", StackRepairAuthority),
        ("U01.stack-ordering-permutations", StackOrderingPermutations),
        ("U01.deletion-bookkeeping", DeletionBookkeeping),
        ("U01.deletion-counter-arithmetic", DeletionCounterArithmetic),
        ("U01.deletion-scan-bounds", DeletionScanBounds),
        ("U01.deletion-sleeping-references", DeletionSleepingReferences),
        ("U01.deletion-guards-and-authority", DeletionGuardsAndAuthority),
        ("U01.deletion-notifications", DeletionNotifications),
        ("U01.deletion-captured-selectors", DeletionCapturedSelectors),
        ("U01.deletion-settlement-check", DeletionSettlementCheck),
        ("U01.deletion-failure-prefixes", DeletionFailurePrefixes),
        ("U01.deletion-nested-activity", DeletionNestedActivity),
        ("U01.deletion-backing-and-signed-owner", DeletionBackingAndSignedOwner),
        ("F06.binary-ordinary-preferences", BinaryOrdinaryPreferences),
        ("F06.binary-multiplayer-preferences", BinaryMultiplayerPreferences),
        ("F06.binary-preference-round-trips", BinaryPreferenceRoundTrips),
        ("F06.binary-preference-prefixes", BinaryPreferencePrefixes),
        ("F06.binary-preference-live-conversion", BinaryPreferenceLiveConversion),
        ("F06.binary-preference-text-and-shape", BinaryPreferenceTextAndShape),
        ("F06.live-map-writers", LiveMapWriters),
        ("F06.live-map-writer-failures", LiveMapWriterFailures),
        ("F06.live-entity-writers", LiveEntityWriters),
        ("F06.live-persistent-writers", LivePersistentWriters),
        ("F06.live-leader-writers", LiveLeaderWriters),
        ("F06.live-layout-writers", LiveLayoutWriters),
        ("F06.live-scenario-multiplayer-writers", LiveScenarioMultiplayerWriters),
        ("F06.live-event-writers", LiveEventWriters),
        ("F06.live-event-failures", LiveEventFailures),
        ("F06.live-save-composition", LiveSaveComposition),
        ("F06.map-input-admission", MapInputAdmission),
        ("F06.map-input-staged-installation", MapInputStagedInstallation),
        ("F06.raw-map-restoration", RawMapRestoration),
        ("F06.game-restoration-values", GameRestorationValues),
        ("F06.game-restoration-legacy-occupancy", GameRestorationLegacyOccupancy),
        ("F06.game-restoration-leader-selection", GameRestorationLeaderSelection),
        ("F06.game-restoration-optional-inputs", GameRestorationOptionalInputs),
        ("F06.game-restoration-admission", GameRestorationAdmission),
        ("N02.message-envelope-bytes", MessageEnvelopeBytes),
        ("N02.message-envelope-admission", MessageEnvelopeAdmission),
        ("N02.argument-envelope-bytes", ArgumentEnvelopeBytes),
        ("N02.message-send-sequencing", MessageSendSequencing),
        ("N02.message-broadcast-live-size", MessageBroadcastLiveSize),
        ("N05.stream-frame-bytes", StreamFrameBytes),
        ("N05.stream-fragmentation", StreamFragmentation),
        ("N05.stream-receive-admission", StreamReceiveAdmission),
        ("N05.stream-oversized-callbacks", StreamOversizedCallbacks),
        ("N05.stream-callback-boundaries", StreamCallbackBoundaries),
        ("N05.stream-send-queue-progress", StreamSendQueueProgress),
        ("N05.stream-send-error-domains", StreamSendErrorDomains),
        ("N05.stream-send-failure-retention", StreamSendFailureRetention),
        ("N05.stream-send-backlog", StreamSendBacklog),
        ("N05.datagram-bytes-and-retention", DatagramBytesAndRetention),
        ("N02.compression-exact-tokens", CompressionExactTokens),
        ("N02.compression-scratch-prefixes", CompressionScratchPrefixes),
        ("N02.compression-safe-word-runs", CompressionSafeWordRuns),
        ("N02.compression-boundaries-and-tails", CompressionBoundariesAndTails),
        ("N02.compression-image-failure-retention", CompressionImageFailureRetention),
        ("N02.compression-word-validation", CompressionWordValidation),
        ("N02.compression-byte-zero-and-tail", CompressionByteZeroAndTail),
        ("N02.compression-receipt-selection", CompressionReceiptSelection),
        ("N02.section-checksums", SectionChecksums),
        ("N02.section-descriptor-and-image-bytes", SectionDescriptorAndImageBytes),
        ("N02.section-registration-and-sizes", SectionRegistrationAndSizes),
        ("N02.section-registration-failure-prefixes", SectionRegistrationFailurePrefixes),
        ("N02.section-mirror-semantics", SectionMirrorSemantics),
        ("N02.section-mirror-failure-prefixes", SectionMirrorFailurePrefixes),
        ("N02.section-image-checksum-ordering", SectionImageChecksumOrdering),
        ("N02.section-image-production-failures", SectionImageProductionFailures),
        ("N02.section-image-sequential-application", SectionImageSequentialApplication),
        ("N02.section-image-application-extents", SectionImageApplicationExtents),
        ("Query.live-query-own-entities", LiveQueryOwnEntities),
        ("Query.live-query-field-domains", LiveQueryFieldDomains),
        ("Query.live-query-owned-snapshots", LiveQueryOwnedSnapshots),
        ("Query.live-query-admission", LiveQueryAdmission),
        ("Query.live-query-no-side-effects", LiveQueryNoSideEffects),
        ("Session.session-private-bindings", SessionPrivateBindings),
        ("Session.session-reassignment-and-lifetimes", SessionReassignmentAndLifetimes),
        ("Session.session-disconnect-and-disposal", SessionDisconnectAndDisposal),
        ("Session.session-retained-ownership", SessionRetainedOwnership),
        ("Session.session-random-domains", SessionRandomDomains),
        ("Session.session-grant-admission", SessionGrantAdmission),
        ("Session.session-world-pins", SessionWorldPins),
        ("Session.command-descriptor-admission", CommandDescriptorAdmission),
        ("Session.command-fifo-and-owned-inputs", CommandFifoAndOwnedInputs),
        ("Session.command-decisions-and-timeouts", CommandDecisionsAndTimeouts),
        ("Session.command-revocation-and-privacy", CommandRevocationAndPrivacy),
        ("Session.command-pending-replacement-and-pins", CommandPendingReplacementAndPins),
        ("Session.command-failures-and-committed-effects", CommandFailuresAndCommittedEffects),
        ("Session.command-invalid-steps-and-reentry", CommandInvalidStepsAndReentry),
        ("Session.command-admission-changes", CommandAdmissionChanges),
        ("Session.command-peer-execution-and-results", CommandPeerExecutionAndResults),
        ("Session.command-peer-admission-and-obligations", CommandPeerAdmissionAndObligations),
        ("Session.command-peer-failure-and-changed-binding", CommandPeerFailureAndChangedBinding),
        ("Session.command-linear-effects-and-retirement", CommandLinearEffectsAndRetirement),
        ("Session.command-pending-effect-rebind", CommandPendingEffectRebind),
        ("Session.command-cleanup-and-allocation-failures", CommandCleanupAndAllocationFailures),
        ("Session.command-controlled-choice-failure", CommandControlledChoiceFailure),
        ("Session.command-retained-choices-across-interaction", CommandRetainedChoicesAcrossInteraction),
        ("Session.command-explicit-continuation", CommandExplicitContinuation),
        ("Session.command-execution-revalidation", CommandExecutionRevalidation),
        ("Session.fresh-game-world-isolation", FreshGameWorldIsolation),
        ("Session.fresh-game-process-allowlist", FreshGameProcessAllowlist),
        ("Session.fresh-game-rejects-pending-work", FreshGameRejectsPendingWork),
        ("Session.fresh-game-rejects-pins-and-resets-again", FreshGameRejectsPinsAndResetsAgain),
        ("F03.rule-state-content-transfer", RuleStateContentTransfer),
        ("F06.game-install-admission", GameInstallAdmission),
        ("F06.game-install-values-and-lifetimes", GameInstallValuesAndLifetimes),
        ("F06.game-install-multiplayer-and-scenario", GameInstallMultiplayerAndScenario),
        ("F06.game-install-name-markers", GameInstallNameMarkers),
        ("F06.game-install-failure-prefixes", GameInstallFailurePrefixes),
        ("F06.game-install-rule-reload-and-layout", GameInstallRuleReloadAndLayout),
        ("F06.game-install-scenario-export-names", GameInstallScenarioExportNames),
        ("F03.preference-file-load-defaults-and-staging", PreferenceFileLoadDefaultsAndStaging),
        ("F03.preference-file-load-failures", PreferenceFileLoadFailures),
        ("F03.preference-file-save-results", PreferenceFileSaveResults),
        ("F03.preference-file-save-exceptions", PreferenceFileSaveExceptions),
        ("F03.preference-file-snapshots-before-io", PreferenceFileSnapshotsBeforeIo),
        ("F03.preference-file-publication-and-isolation", PreferenceFilePublicationAndIsolation),
        ("P04.classic-label-catalog", ClassicLabelCatalog),
        ("P04.classic-label-fallback", ClassicLabelFallback),
        ("P04.classic-label-missing-and-retained-names", ClassicLabelMissingAndRetainedNames),
        ("P04.classic-label-admission-and-cleanup", ClassicLabelAdmissionAndCleanup),
        ("P04.label-installation-independent-names", LabelInstallationIndependentNames),
        ("P04.text-language-configuration", TextLanguageConfiguration),
        ("F06.game-file-load-versions", GameFileLoadVersions),
        ("F06.game-file-load-rejected-inputs", GameFileLoadRejectedInputs),
        ("F06.game-file-load-guards-and-paths", GameFileLoadGuardsAndPaths),
        ("F06.game-file-load-failures-and-publication", GameFileLoadFailuresAndPublication),
        ("F06.game-file-save-complete-image", GameFileSaveCompleteImage),
        ("F06.game-file-save-scenario-and-events", GameFileSaveScenarioAndEvents),
        ("F06.game-file-save-failures", GameFileSaveFailures),
        ("F06.game-file-save-admission-and-hotseat", GameFileSaveAdmissionAndHotseat),
        ("F06.game-file-live-roundtrip-and-opaque-lifetimes", GameFileLiveRoundtripAndOpaqueLifetimes),
        ("F03.classic-rule-context-selection-and-ownership", ClassicRuleContextSelectionAndOwnership),
        ("F03.classic-rule-context-failure-cleanup", ClassicRuleContextFailureCleanup),
        ("F05.world-size-presets", WorldSizePresets),
        ("F05.world-size-decimal-and-boundaries", WorldSizeDecimalAndBoundaries),
        ("F05.world-size-cancellation-and-failures", WorldSizeCancellationAndFailures),
        ("F05.world-parameters-random-order-and-law", WorldParametersRandomOrderAndLaw),
        ("F05.world-parameters-ordinary-persistence", WorldParametersOrdinaryPersistence),
        ("F05.world-parameters-cancellation-prefixes", WorldParametersCancellationPrefixes),
        ("F05.world-parameters-failure-prefixes", WorldParametersFailurePrefixes),
        ("F05.world-parameters-persistence-failures-and-live-choices", WorldParametersPersistenceFailuresAndLiveChoices),
        ("F05.world-setup-publication-and-pins", WorldSetupPublicationAndPins),
        ("F05.world-setup-admission-and-inactive-use", WorldSetupAdmissionAndInactiveUse),
        ("F05.setup-calendar-retention", SetupCalendarRetention),
        ("F05.setup-calendar-scenario-and-magnitude", SetupCalendarScenarioAndMagnitude),
        ("F05.premade-installation-and-retention", PremadeInstallationAndRetention),
        ("F05.premade-normalization-and-hint-scan", PremadeNormalizationAndHintScan),
        ("F05.premade-seed-and-response-laws", PremadeSeedAndResponseLaws),
        ("F05.premade-live-choices-and-failures", PremadeLiveChoicesAndFailures),
        ("F05.premade-input-failures-and-staging", PremadeInputFailuresAndStaging),
        ("F06.premade-transfer-ownership", PremadeTransferOwnership),
        ("F05.premade-admission-and-publication", PremadeAdmissionAndPublication),
        ("F05.premade-parameter-dispatch", PremadeParameterDispatch),
        ("F05.new-game-reset-and-retained-records", NewGameResetAndRetainedRecords),
        ("F05.new-game-bounds-map-and-identity-retention", NewGameBoundsMapAndIdentityRetention),
        ("F05.new-game-live-child-order", NewGameLiveChildOrder),
        ("F05.new-game-options-rates-and-masks", NewGameOptionsRatesAndMasks),
        ("F05.new-game-failure-prefixes", NewGameFailurePrefixes),
        ("F05.new-game-publication-random-and-admission", NewGamePublicationRandomAndAdmission),
        ("F05.player-setup-ordinary-and-restored-names", PlayerSetupOrdinaryAndRestoredNames),
        ("F05.player-setup-cancellation-routes", PlayerSetupCancellationRoutes),
        ("F05.player-setup-random-branches-and-retries", PlayerSetupRandomBranchesAndRetries),
        ("F05.player-setup-advanced-combinations", PlayerSetupAdvancedCombinations),
        ("F05.player-setup-acceleration-retention", PlayerSetupAccelerationRetention),
        ("F05.player-setup-name-children", PlayerSetupNameChildren),
        ("F05.player-setup-name-cancellation-and-unavailable", PlayerSetupNameCancellationAndUnavailable),
        ("F05.player-setup-name-bounds-and-partial-copies", PlayerSetupNameBoundsAndPartialCopies),
        ("F05.player-setup-opponent-retries-and-roster", PlayerSetupOpponentRetriesAndRoster),
        ("F05.player-setup-admission-and-publication", PlayerSetupAdmissionAndPublication),
        ("F05.world-entry-order-and-preference-saves", WorldEntryOrderAndPreferenceSaves),
        ("F05.world-entry-cancellation-prefixes", WorldEntryCancellationPrefixes),
        ("F05.world-entry-premade-and-owned-rules", WorldEntryPremadeAndOwnedRules),
        ("F05.world-entry-acceleration-and-child-failures", WorldEntryAccelerationAndChildFailures),
        ("F05.world-entry-isolation-and-classic-scratch", WorldEntryIsolationAndClassicScratch),
        ("F05.world-entry-admission-and-inactive-pins", WorldEntryAdmissionAndInactivePins),
        ("F05.world-entry-introduction-and-common-source", WorldEntryIntroductionAndCommonSource),
        ("F06.file-choice-types-and-admission", FileChoiceTypesAndAdmission),
        ("F06.save-menu-owned-notices-and-formatting", SaveMenuOwnedNoticesAndFormatting),
        ("F06.save-menu-names-and-mode-notices", SaveMenuNamesAndModeNotices),
        ("F06.save-menu-cancellation-and-output-failures", SaveMenuCancellationAndOutputFailures),
        ("F06.save-menu-deferred-acceptance-and-pin", SaveMenuDeferredAcceptanceAndPin),
        ("F06.save-menu-deferred-cleanup-and-failure-precedence", SaveMenuDeferredCleanupAndFailurePrecedence),
        ("F06.save-menu-admission-and-provider-failures", SaveMenuAdmissionAndProviderFailures),
        ("F06.save-menu-pending-commands-remain-responsive", SaveMenuPendingCommandsRemainResponsive),
        ("F06.loaded-units-scan-and-repair", LoadedUnitsScanAndRepair),
        ("F06.loaded-units-live-tail-and-failure", LoadedUnitsLiveTailAndFailure),
        ("F06.load-dialog-cancellation-and-admission", LoadDialogCancellationAndAdmission),
        ("F06.load-dialog-core-statuses", LoadDialogCoreStatuses),
        ("F06.load-dialog-alternative-choices", LoadDialogAlternativeChoices),
        ("F06.load-dialog-alternative-bounds", LoadDialogAlternativeBounds),
        ("F06.load-dialog-events-and-options", LoadDialogEventsAndOptions),
        ("F06.load-dialog-live-children", LoadDialogLiveChildren),
        ("F06.load-dialog-cleanup-and-publication", LoadDialogCleanupAndPublication),
        ("F06.load-dialog-failure-prefixes", LoadDialogFailurePrefixes),
        ("U01.operation-remote-primitives", UnitOperationRemotePrimitives),
        ("U01.operation-timeout-drain-and-markers", UnitOperationTimeoutDrainAndMarkers),
        ("U01.operation-cancellation-and-failures", UnitOperationCancellationAndFailures),
        ("U01.operation-callback-cancellation", UnitOperationCallbackCancellation),
        ("U01.operation-local-mechanics-and-guards", UnitOperationLocalMechanicsAndGuards),
        ("U01.operation-admission-and-raw-arguments", UnitOperationAdmissionAndRawArguments),
        ("U01.operation-stack-successors-and-authority", UnitOperationStackSuccessorsAndAuthority),
        ("U01.operation-stack-timeout-and-partial-failure", UnitOperationStackTimeoutAndPartialFailure),
        ("U01.operation-command-composition", UnitOperationCommandComposition),
        ("P5.local-unit-confirmation", LocalUnitCommandConfirmation),
        ("P5.local-unit-cancellation", LocalUnitCommandCancellation),
        ("P5.local-unit-stale-target", LocalUnitCommandStaleTarget),
        ("P5.local-unit-admission", LocalUnitCommandAdmission),
        ("P5.local-unit-finite-bounds", LocalUnitCommandFiniteBounds),
        ("P5.client-model-command-parity", ClientModelCommandParity),
        ("P5.client-model-privacy-rebind", ClientModelPrivacyAndRebind),
        ("P5.client-model-cancel-revoke", ClientModelCancellationAndRevocation),
        ("P5.console-typed-grammar", CommandTextTypedGrammar),
        ("P5.console-admission", CommandTextRejectsWithoutSubmission),
        ("M2.document-envelope", DocumentEnvelope),
        ("M2.document-workflow", DocumentWorkflow),
        ("M2.clock-and-silent-media", ClockAndSilentMedia),
        ("M2.wave-pcm-samples", WavePcmSamples),
        ("M2.wave-malformed-inputs", WaveMalformedInputs),
        ("M2.shell-action-availability", ShellActionAvailability),
        ("M2.dialog-identity-lifecycle", DialogIdentityLifecycle),
        ("M2.dialog-target-revalidation", DialogTargetRevalidation),
        ("M2.dialog-deadline-and-peer-traffic", DialogDeadlineAndPeerTraffic),
        ("M1.random-checkpoint-generated-domains", RandomCheckpointGeneratedDomains),
        ("M1.random-checkpoint-controlled-keys-and-prefix", RandomCheckpointControlledKeysAndPrefix),
        ("M1.random-checkpoint-trace-capacity-and-replay", RandomCheckpointTraceCapacityAndReplay),
        ("M1.random-checkpoint-malformed-admission", RandomCheckpointMalformedAdmission),
        ("M1.random-checkpoint-resource-limits", RandomCheckpointResourceLimits),
        ("M1.local-checkpoint-codec-round-trips", LocalCheckpointCodecRoundTrips),
        ("M1.local-checkpoint-codec-rng", LocalCheckpointCodecRng),
        ("M1.local-checkpoint-codec-malformed-envelope", LocalCheckpointCodecMalformedEnvelope),
        ("M1.local-checkpoint-codec-malformed-payload", LocalCheckpointCodecMalformedPayload),
        ("M1.local-checkpoint-codec-resource-limits", LocalCheckpointCodecResourceLimits),
        ("M1.session-checkpoint-idle", SessionCheckpointIdle),
        ("M1.session-checkpoint-resumes-once", SessionCheckpointResumesOnce),
        ("M1.session-checkpoint-cancel-timeout", SessionCheckpointCancelTimeout),
        ("M1.session-checkpoint-bad-admission", SessionCheckpointBadAdmission),
        ("M1.session-checkpoint-unsupported-state", SessionCheckpointUnsupportedState),
        ("M1.session-checkpoint-bindings", SessionCheckpointBindings),
        ("M1.session-checkpoint-peer-work", SessionCheckpointPeerWork),
        ("M1.session-checkpoint-owner-reentry-cleanup", SessionCheckpointOwnerReentryAndCleanup)
    ];

    public static IEnumerable<string> Names => Cases.Select(c => c.Name);
    public static void Run(string name) => Cases.Single(c => c.Name == name).Run();

    public static string RunJson(Assembly host)
    {
        CaseResult[] results = Cases.Select(c =>
        {
            try { c.Run(); return new CaseResult(c.Name, true, null); }
            catch (Exception error) { return new CaseResult(c.Name, false, error.ToString()); }
        }).ToArray();
        var report = new FoundationReport("openciv2-managed-foundations/2", RandomPolicy.Id,
            RandomPolicy.Version, results.All(r => r.Passed), results,
            new[] { typeof(Arithmetic).Assembly, typeof(CoreFile).Assembly, typeof(CommandRequest).Assembly,
                typeof(Geometry).Assembly, typeof(StoredSave).Assembly, typeof(StoredSnapshot).Assembly, typeof(Messages).Assembly, typeof(OpenCiv2.Session.SessionRuntime).Assembly, typeof(OpenCiv2.ViewModels.SessionPanel).Assembly, typeof(OpenCiv2.Console.CommandText).Assembly, typeof(FoundationSuite).Assembly, host }.Distinct().Select(BuildIdentity.Read).ToArray());
        return JsonSerializer.Serialize(report, ReportJson.Default.FoundationReport);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void IntegerWrap()
    {
        Equal(-1, Arithmetic.Signed32(uint.MaxValue));
        Equal(int.MinValue, Arithmetic.Add32(int.MaxValue, 1));
        Equal(int.MaxValue, Arithmetic.Subtract32(int.MinValue, 1));
        Equal(-2, Arithmetic.Multiply32(int.MaxValue, 2));
    }

    private static void SignedDivision()
    {
        Equal(-2, Arithmetic.Divide32(-7, 3));
        Equal(-1, Arithmetic.Remainder32(-7, 3));
        Equal(-2, Arithmetic.Divide32(7, -3));
        Equal(1, Arithmetic.Remainder32(7, -3));
        Throws<DivideByZeroException>(() => Arithmetic.Divide32(1, 0));
        Throws<DivideByZeroException>(() => Arithmetic.Remainder32(1, 0));
        Throws<OverflowException>(() => Arithmetic.Divide32(int.MinValue, -1));
        Throws<OverflowException>(() => Arithmetic.Remainder32(int.MinValue, -1));
    }

    private static void ClampsAndShifts()
    {
        Equal(0, Arithmetic.ClampAB(-5, 10, 0));
        Equal(10, Arithmetic.ClampC(-5, 10, 0));
        Equal(0, Arithmetic.ClampC(5, 10, 0));
        Equal(-4, Arithmetic.ShiftSigned(-7, -1));
        Equal(int.MinValue, Arithmetic.ShiftSigned(0x40000000, 1));
        foreach (int count in new[] { -32, 32, int.MinValue })
            Equal(-7, Arithmetic.ShiftSigned(-7, count));
        Equal(-1, Arithmetic.ShiftSigned(int.MinValue, -31));
        Equal(0, Arithmetic.ShiftSigned(int.MaxValue, -31));
    }

    private static void WrappingExpressions()
    {
        var maximum = new WrappingI32(int.MaxValue);
        Equal(int.MinValue, (int)(maximum + new WrappingI32(1)));
        Equal(-1, (int)((maximum * new WrappingI32(2)) / new WrappingI32(2)));
        Equal(int.MinValue, (int)-new WrappingI32(int.MinValue));
        Equal(-1, (int)(WrappingI32)ulong.MaxValue);
        Equal(uint.MaxValue, (uint)(WrappingI32)(-1));
    }

    private static void PrimaryPopulation()
    {
        int[] counts = new int[3];
        for (int code = 0; code <= 32767; ++code)
        {
            ++counts[Arithmetic.PrimaryModuloFromCode(code, 3)];
            Equal(code, Arithmetic.PrimaryModuloFromCode(code, int.MaxValue));
        }
        Equal(10923, counts[0]); Equal(10923, counts[1]); Equal(10922, counts[2]);
        Throws<ArgumentOutOfRangeException>(() => Arithmetic.PrimaryModuloFromCode(-1, 3));
        Throws<ArgumentOutOfRangeException>(() => Arithmetic.PrimaryModuloFromCode(32768, 3));
        Throws<ArgumentOutOfRangeException>(() => Arithmetic.PrimaryModuloFromCode(0, 0));
    }

    private static void SecondaryBinary64()
    {
        // Nearest-even binary64 images of F01 §4's rational U / (2^32 - 1).
        (uint Code, long Bits)[] vectors =
        [
            (0, 0), (1, 0x3DF0000000100000), (1015568748, 0x3FCE442CB61E442D),
            (2147483648, 0x3FE0000000100000), (4294967294, 0x3FEFFFFFFFE00000),
            (uint.MaxValue, 0x3FF0000000000000)
        ];
        foreach (var (code, bits) in vectors)
            Equal(bits, BitConverter.DoubleToInt64Bits(Arithmetic.SecondaryUnitFromCode(code)));
    }

    private static void SecondaryRange()
    {
        (int Lo, int Hi, uint Code, int Result)[] vectors =
        [
            (0, 2, 1015568748, 0), (2, 0, 1015568748, 0), (5, 5, 0, 5),
            (-4, 7, 1015568748, -2), (-4, 7, 0, -4), (-4, 7, uint.MaxValue, 7),
            (int.MinValue, int.MaxValue, 1015568748, int.MinValue),
            (int.MinValue, 1, 1015568748, 1639699275),
            (1, int.MinValue, 1015568748, 1639699275),
            (0, int.MaxValue, uint.MaxValue, int.MaxValue),
            (int.MinValue, 0, uint.MaxValue, 1)
        ];
        foreach (var (lo, hi, code, result) in vectors)
            Equal(result, Arithmetic.SecondaryRangeFromCode(lo, hi, code));
    }

    private static void ClockAndMapSeed()
    {
        (uint Milliseconds, uint Ticks, ushort Seed)[] vectors =
        [(0, 0, 1), (16, 0, 1), (17, 1, 1), (1000, 60, 60),
         (715827882, 42949672, 23592), (715827883, 0, 1), (uint.MaxValue, 42949672, 23592)];
        foreach (var (milliseconds, ticks, seed) in vectors)
        {
            Equal(ticks, Arithmetic.GameTicksFromMilliseconds(milliseconds));
            Equal(seed, Arithmetic.MapPatternSeedFromTicks(ticks));
        }
        Equal((ushort)1, Arithmetic.MapPatternSeedFromTicks(32768));
    }

    private static void PcgReferenceVector()
    {
        // PCG author's minimal C demo: initstate=42, initseq=54, XSH-RR 64/32.
        uint[] expected = [0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E];
        var source = new Pcg32Source(42, 54);
        foreach (uint word in expected)
            Equal(word, source.NextUInt32());
        Equal(6UL, source.Capture().Draws);
        var highBit = new Pcg32Source(42, 54 | (1UL << 63));
        Equal(expected[0], highBit.NextUInt32());
    }

    private static void ContextPolicyVector()
    {
        var initial = RandomPolicy.InitialPosition(0, RandomDomain.Gameplay, "session/main");
        // Fixed seed/stream vectors are filled from an independent policy calculation.
        Equal(PolicyVectors.StateSeed, initial.Seed);
        Equal(PolicyVectors.Stream, initial.Stream);
        var game = new GameplayRandom(0, "session/main");
        var checkpoint = game.Capture();
        foreach (uint expected in PolicyVectors.Words)
            Equal(expected, game.SecondaryCode("F01.vector"));
        game.Restore(checkpoint, 0);
        foreach (uint expected in PolicyVectors.Words)
            Equal((int)(expected >> 17), game.PrimaryCode("F01.vector"));
    }

    private static void DomainIsolation()
    {
        var game = new GameplayRandom(17, "session/main");
        var same = new GameplayRandom(17, "session/main");
        var presentation = new PresentationRandom(17, "session/main");
        var cosmetic = new CosmeticRandom(17, "session/main");
        Equal(false, game.Capture().Position == presentation.Capture().Position);
        Equal(false, presentation.Capture().Position == cosmetic.Capture().Position);
        for (int i = 0; i < 100; ++i)
        {
            presentation.SecondaryUnit("P07.report");
            cosmetic.PrimaryModulo("UI.paint", 7);
            Equal(same.SecondaryRange("F01.test", -4, 7), game.SecondaryRange("F01.test", -4, 7));
        }
        var before = presentation.Capture();
        cosmetic.SecondaryCode("UI.extra");
        Equal(before, presentation.Capture());
    }

    private static void CaptureRestore()
    {
        var game = new GameplayRandom(123, "session/main");
        for (int i = 0; i < 37; ++i) game.SecondaryCode("F01.prefix");
        var checkpoint = game.Capture();
        uint[] expected = Enumerable.Range(0, 700).Select(_ => game.SecondaryCode("F01.suffix")).ToArray();
        var independent = new GameplayRandom(999, "session/main");
        independent.Restore(checkpoint, 37);
        game.Restore(checkpoint, 37);
        foreach (uint word in expected)
        {
            Equal(word, game.SecondaryCode("F01.suffix"));
            Equal(word, independent.SecondaryCode("F01.suffix"));
        }
        Equal(37UL, checkpoint.Position.Draws);
        Equal(737UL, game.Draws);
    }

    private static void RejectionPreservesState()
    {
        var game = new GameplayRandom(0, "session/main");
        game.SecondaryCode("F01.prefix");
        var before = game.Capture();
        Throws<ArgumentOutOfRangeException>(() => game.PrimaryModulo("F01.invalid", 0));
        Throws<ArgumentException>(() => game.SecondaryCode(""));
        Throws<ArgumentException>(() => game.Restore(before, 0));
        Throws<ArgumentException>(() => game.Restore(before with { PolicyId = "oc2-mt19937-64" }, 1));
        Throws<ArgumentException>(() => game.Restore(before with { PolicyVersion = 2 }, 1));
        Throws<ArgumentException>(() => game.Restore(before with { Domain = RandomDomain.Cosmetic }, 1));
        Throws<ArgumentException>(() => game.Restore(before with { Context = "other" }, 1));
        Throws<ArgumentException>(() => game.Restore(before with { Position = new(0, ulong.MaxValue, 0) }, 1));
        Throws<ArgumentException>(() => new GameplayRandom(0, ""));
        Throws<ArgumentException>(() => new GameplayRandom(0, "é"));
        Equal(before, game.Capture());
    }

    private static void ConstantDecisions()
    {
        var game = new GameplayRandom(0, "session/main");
        var before = game.Capture();
        Equal(0, game.PrimaryModulo("", 1));
        Equal(9, game.SecondaryRange("", 9, 9));
        Equal(before, game.Capture());
    }

    private static void CultureIndependence()
    {
        var before = RandomPolicy.InitialPosition(ulong.MaxValue, RandomDomain.Cosmetic, "UI/I");
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            // A custom number format works even with invariant-globalization WASM.
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;
            Equal(before, RandomPolicy.InitialPosition(ulong.MaxValue, RandomDomain.Cosmetic, "UI/I"));
            Equal(-2, Arithmetic.SecondaryRangeFromCode(-4, 7, 1015568748));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}

public sealed record CaseResult(string Name, bool Passed, string? Error);
public sealed record FoundationReport(string Format, string Policy, int PolicyVersion, bool Passed, CaseResult[] Cases, CompiledInputs[] Build);

[JsonSerializable(typeof(FoundationReport))]
public partial class ReportJson : JsonSerializerContext;
