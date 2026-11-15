export module openciv.session.configuration_types;

import std;

export namespace openciv::session {
// F03 section 8 accepted checkbox values; presentation scratch is separate.
// Game order: sound, music, wait, autosave, enemy moves, skip enemy pauses,
// fast slide, advice, tutorial, mouse goto, Enter closes city.
using GameOptionChoices = std::array<bool, 11>;
// Graphics order: throne, diplomacy, heralds, advance Civilopedia, council,
// wonder movies. Message order: growth halted, improvement built, unit built,
// invalid build, disorder, order restored, celebration, food low, pollution,
// production penalty, zoom-to-city. Checked message choices mean enabled.
using GraphicsOptionChoices = std::array<bool, 6>;
using MessageOptionChoices = std::array<bool, 11>;
class MusicControl {
public:
    MusicControl() = default;
    virtual ~MusicControl() = default;
    MusicControl(const MusicControl&) = delete;
    MusicControl& operator=(const MusicControl&) = delete;
    // P10 reinitialization includes its stop/close/open/layout sequence;
    // synchronization is a separate child operation.
    virtual void stop() const = 0;
    virtual void reinitialize() const = 0;
    virtual void synchronize() const = 0;
};
// Clear chat on new game, clear chat on boot, double production, double movement.
using MultiplayerOptionChoices = std::array<bool, 4>;
struct OptionProposal {
    std::string sender, description;
    bool production = false, movement = false;
    std::int32_t expected_votes = 0;
};
struct OptionVoteProgress {
    std::int32_t pending = 0, status = 0;
};
// Owned N02 vote registration/transport. Each poll does bounded, nonblocking
// work; presentation observes the operation instead of running a modal loop.
class OptionVoteChannel {
public:
    OptionVoteChannel() = default;
    virtual ~OptionVoteChannel() = default;
    OptionVoteChannel(const OptionVoteChannel&) = delete;
    OptionVoteChannel& operator=(const OptionVoteChannel&) = delete;
    virtual void begin(OptionProposal proposal) = 0;
    virtual OptionVoteProgress poll() = 0;
    virtual void cancel() noexcept = 0;
};
class OptionBroadcaster {
public:
    OptionBroadcaster() = default;
    virtual ~OptionBroadcaster() = default;
    OptionBroadcaster(const OptionBroadcaster&) = delete;
    OptionBroadcaster& operator=(const OptionBroadcaster&) = delete;
    virtual void rejected() const = 0;
    virtual void accepted() const = 0;
    virtual void units() const = 0;
    virtual void options(bool production, bool movement, bool simultaneous) const = 0;
};
enum class MultiplayerOptionResult { cancelled, client_only, unchanged, rejected, applied, failed };
} // namespace openciv::session
