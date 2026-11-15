export module openciv.session.classic_save_menu;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_file_choice;
export import openciv.session.classic_save;
export import openciv.session.runtime;

export namespace openciv::session {
struct ClassicSaveMenuState {
    bool session_active = false;
    std::string filename;
};
struct LocalCalendar {
    std::int32_t month = 0, day = 0, year = 0, hour = 0, minute = 0, second = 0;
};
struct SaveNotice {
    std::string key;
    std::array<std::string, 5> arguments;
};
class CountdownControl {
public:
    CountdownControl() = default;
    virtual ~CountdownControl() = default;
    CountdownControl(const CountdownControl&) = delete;
    CountdownControl& operator=(const CountdownControl&) = delete;
    virtual void stop(bool notify) const = 0;
    virtual void start(bool notify) const = 0;
};
struct SaveMenuDependencies {
    const io::FileStorage* output = nullptr;
    // N03 start is distinct from resume. These are real timer boundaries.
    const CountdownControl* countdown = nullptr;
    // Pure presentation queries. They do not mutate game state or admit reentry.
    base::CallbackRef<LocalCalendar()> local_time;
    base::CallbackRef<std::string(std::string_view)> lowercase;
    FileLabel label;
    base::CallbackRef<std::string(std::int32_t)> ruler_name, ruler_title, civilization_plural;
    base::CallbackRef<std::string(std::int32_t)> format_year;
    base::CallbackRef<std::string()> error_text;
    base::CallbackRef<void(const SaveNotice&)> notice;
};
enum class SaveMenuResult { inactive, cancelled, saved, failed };
// F06 asynchronous owner workflow. The operation owns its chooser, request and
// borrowed dependency handles; runtime/files/menu, providers and callable owners
// must outlive it. advance performs one phase and returns nullopt while the chooser is open.
// Cancellation/destruction dismisses the chooser and restarts a stopped timer
// once. Explicit completion propagates failures; destruction is best-effort.
// No participant binding is invented for application lifecycle work.
class ClassicSaveOperation {
public:
    ClassicSaveOperation(SessionRuntime& runtime, ClassicFileState& files,
                         ClassicSaveMenuState& menu, bool scenario, SaveMenuDependencies services,
                         std::unique_ptr<FileChooser> chooser);
    ~ClassicSaveOperation();
    ClassicSaveOperation(const ClassicSaveOperation&) = delete;
    ClassicSaveOperation& operator=(const ClassicSaveOperation&) = delete;
    std::optional<SaveMenuResult> advance();
    SaveMenuResult cancel();
    const std::optional<SaveMenuResult>& result() const noexcept { return result_; }

private:
    FileChoiceRequest prepare_request();
    SaveMenuResult accept(FileChoiceResult choice);
    void finish(SaveMenuResult result);
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
// F06 production save UI. Captured presentation values and owned notice arguments;
// only an accepted path starts saving. Every completed timer stop has one restart
// attempt, including cancellation, chooser errors and exceptional providers.
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuDependencies& services, FileChoiceCallback chooser);
} // namespace openciv::session
