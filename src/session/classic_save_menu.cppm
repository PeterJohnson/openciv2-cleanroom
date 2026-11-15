export module openciv.session.classic_save_menu;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_file_choice;
export import openciv.session.classic_save;
export import openciv.session.runtime;

export namespace openciv::session {
/// Retained save-menu defaults and selections across invocations.
struct ClassicSaveMenuState {
    bool session_active = false;
    std::string filename;
};
/// Owner-supplied local calendar values used to construct default save names.
struct LocalCalendar {
    std::int32_t month = 0, day = 0, year = 0, hour = 0, minute = 0, second = 0;
};
/// The semantic content of a save-workflow notice.
struct SaveNotice {
    std::string key;
    std::array<std::string, 5> arguments;
};
/// Borrowed control of the multiplayer countdown around save interactions.
class CountdownControl {
public:
    CountdownControl() = default;
    virtual ~CountdownControl() = default;
    CountdownControl(const CountdownControl&) = delete;
    CountdownControl& operator=(const CountdownControl&) = delete;
    /// Stop the countdown, optionally notifying its observers.
    virtual void stop(bool notify) const = 0;
    /// Restart the countdown, optionally notifying its observers.
    virtual void start(bool notify) const = 0;
};
/// Explicit storage, naming, calendar, countdown, and notice services for saving.
struct SaveMenuDependencies {
    const io::FileStorage* output = nullptr;
    // N03 start is distinct from resume. These are real timer boundaries.
    const CountdownControl* countdown = nullptr;
    /// Pure presentation queries. They do not mutate game state or admit reentry.
    base::CallbackRef<LocalCalendar()> local_time;
    base::CallbackRef<std::string(std::string_view)> lowercase;
    FileLabel label;
    base::CallbackRef<std::string(std::int32_t)> ruler_name, ruler_title, civilization_plural;
    base::CallbackRef<std::string(std::int32_t)> format_year;
    base::CallbackRef<std::string()> error_text;
    base::CallbackRef<void(const SaveNotice&)> notice;
};
/// Terminal outcome of the save-menu workflow.
enum class SaveMenuResult { inactive, cancelled, saved, failed };
/// F06 asynchronous owner workflow. The operation owns its chooser, request and
/// borrowed dependency handles; runtime/files/menu, providers and callable owners
/// must outlive it. advance performs one phase and returns nullopt while the chooser is open.
/// Cancellation/destruction dismisses the chooser and restarts a stopped timer
/// once. Explicit completion propagates failures; destruction is best-effort.
/// No participant binding is invented for application lifecycle work.
class ClassicSaveOperation {
public:
    /// Start a save workflow with an owned chooser and a pinned runtime world.
    ClassicSaveOperation(SessionRuntime& runtime, ClassicFileState& files,
                         ClassicSaveMenuState& menu, bool scenario, SaveMenuDependencies services,
                         std::unique_ptr<FileChooser> chooser);
    /// Release workflow resources and abandon any unfinished interaction safely.
    ~ClassicSaveOperation();
    ClassicSaveOperation(const ClassicSaveOperation&) = delete;
    ClassicSaveOperation& operator=(const ClassicSaveOperation&) = delete;
    /// Advance the chooser/save workflow and return a result once complete.
    std::optional<SaveMenuResult> advance();
    /// Cancel the workflow and return its terminal result.
    SaveMenuResult cancel();
    /// Inspect the retained terminal result, if the operation has finished.
    const std::optional<SaveMenuResult>& result() const noexcept { return result_; }

private:
    /// Build a file-choice request from the current save mode and naming inputs.
    FileChoiceRequest prepare_request();
    /// Apply an accepted choice and perform the corresponding save.
    SaveMenuResult accept(FileChoiceResult choice);
    /// Record completion and release workflow-owned state.
    void finish(SaveMenuResult result);
    /// Perform nonthrowing cleanup of an unfinished save operation.
    void abandon() noexcept;
    SessionRuntime& runtime_;
    ClassicFileState& files_;
    ClassicSaveMenuState& menu_;
    bool scenario_, started_ = false, stopped_ = false, advancing_ = false;
    SaveMenuDependencies services_;
    std::unique_ptr<FileChooser> chooser_;
    std::optional<SessionActivity> activity_;
    std::optional<SaveMenuResult> result_;
};
/// F06 production save UI. Captured presentation values and owned notice arguments;
/// only an accepted path starts saving. Every completed timer stop has one restart
/// attempt, including cancellation, chooser errors and exceptional providers.
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuDependencies& services, FileChoiceCallback chooser);
} // namespace openciv::session
