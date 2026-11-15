#include <catch2/catch_test_macros.hpp>

import std;
import openciv.formats.classic_text_reader;

// Independently authored F03 section 2/9 and Appendix A inputs and expectations.

using namespace openciv::formats::classic;
namespace {
struct Trace {
    unsigned reads = 0, closes = 0;
    std::vector<std::size_t> seeks;
    std::optional<TextRead> override_read;
    bool fail_seek = false, fail_close = false;
};
class TracedStream final : public TextStream {
public:
    TracedStream(std::string bytes, std::shared_ptr<Trace> trace)
        : memory_(std::move(bytes)), trace_(std::move(trace)) {}
    TextRead read() override {
        ++trace_->reads;
        return trace_->override_read ? *trace_->override_read : memory_.read();
    }
    bool seek(std::size_t offset) override {
        trace_->seeks.push_back(offset);
        return !trace_->fail_seek && memory_.seek(offset);
    }
    void close() override {
        ++trace_->closes;
        memory_.close();
        if (trace_->fail_close)
            throw std::runtime_error("close failed");
    }

private:
    MemoryTextStream memory_;
    std::shared_ptr<Trace> trace_;
};
struct Fixture {
    TextWorkspace workspace;
    TextDirectories directories{"caller", "scenario", "home", "txt"};
    std::map<std::string, std::string> files;
    std::map<std::string, std::shared_ptr<Trace>> traces;
    std::vector<std::string> opens;
    TextFiles services() {
        return {[this](std::string_view directory, std::string_view path) -> OpenedText {
                    REQUIRE(directory == directories.working);
                    const auto key = std::string(directory) + "/" + std::string(path);
                    opens.push_back(key);
                    const auto file = files.find(key);
                    if (file == files.end())
                        return {};
                    auto& trace = traces[key];
                    if (!trace)
                        trace = std::make_shared<Trace>();
                    return {key, std::make_unique<TracedStream>(file->second, trace)};
                },
                {}};
    }
    TextReader reader() { return {workspace, directories, services(), ascii_text_case()}; }
};
template <class Action> void fault(Action action, TextFault expected) {
    try {
        action();
        FAIL("Expected a bounded classic text failure");
    } catch (const TextError& error) {
        REQUIRE(error.fault == expected);
    }
}
} // namespace

TEST_CASE("F03 text reads count translated bytes and split long physical lines",
          "[classic][text]") {
    MemoryTextStream stream{std::string(253, 'a') + "\r\n" + std::string(254, 'b') + "Z\n\rX\r\n"};
    auto line = stream.read();
    REQUIRE(line.status == TextReadStatus::line);
    REQUIRE(line.characters == std::string(253, 'a') + "\n");
    REQUIRE(stream.position() == 255);
    REQUIRE(stream.read().characters == std::string(254, 'b'));
    REQUIRE(stream.position() == 509);
    REQUIRE(stream.read().characters == "Z\n");
    REQUIRE(stream.read().characters == "\rX\n");
    REQUIRE(stream.read().status == TextReadStatus::end);
    REQUIRE(stream.seek(253));
    REQUIRE(stream.read().characters == "\n");
    REQUIRE_FALSE(stream.seek(1000));
    stream.close();
    REQUIRE_FALSE(stream.seek(0));
    fault([&] { stream.read(); }, TextFault::unavailable_stream);
}
TEST_CASE("F03 lines trim only spaces and tabs without skipping comments or headers",
          "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = " \t a , b \t\r\n\n; comment\n@OTHER\n\vname\r\n\xef\xbb\xbf@A";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", std::nullopt));
    for (const auto expected : {"a , b", "", "; comment", "@OTHER", "\vname", "\xef\xbb\xbf@A"}) {
        REQUIRE(reader.next_line());
        REQUIRE(f.workspace.line() == expected);
        REQUIRE(f.workspace.cursor == 0);
    }
    REQUIRE_FALSE(reader.next_line());
    REQUIRE(f.workspace.line().empty());
    REQUIRE_FALSE(reader.is_open());
}
TEST_CASE("F03 tokenization retains empty fields punctuation and long names", "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = " a , ,b;tail,\n\"a,b\",x\n,,Caf\xe9\n" + std::string(254, 'n');
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", std::nullopt));
    REQUIRE(reader.next_line());
    for (const auto expected : {"a", "", "b;tail", "", ""})
        REQUIRE(reader.next_token() == expected);
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "\"a");
    REQUIRE(reader.next_token() == "b\"");
    REQUIRE(reader.next_token() == "x");
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_name(true) == " ");
    REQUIRE(reader.next_name(false).empty());
    REQUIRE(reader.next_name(true) == "Caf\xe9");
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_name(true) == std::string(254, 'n'));
    REQUIRE(f.workspace.cursor == 254);
    REQUIRE(reader.next_token().empty());
    REQUIRE(f.workspace.cursor == 254);
}
TEST_CASE("F03 integer syntax wraps unsigned accumulation and decimal negation",
          "[classic][text]") {
    const std::vector<std::pair<std::string, std::int32_t>> cases{
        {"", 0},
        {"bad", 0},
        {"-0x10", 0},
        {"0xG", 0},
        {"0x", 0},
        {"0xFFFFFFFF", -1},
        {"0XfF; note", 255},
        {"0x100000001", 1},
        {"0x+10", 0},
        {"0x 10", 0},
        {"0b101x11", 5},
        {"0B11", 3},
        {"0b-1", 0},
        {"0b 1", 0},
        {"0d\v+009x", 9},
        {"0D-1", -1},
        {"0d0x10", 0},
        {"0012", 12},
        {"12 ; note", 12},
        {"\t\n\v\f\r -1", -1},
        {"-\t1", 0},
        {"\xa0"
         "1",
         0},
        {"2147483648", std::numeric_limits<std::int32_t>::min()},
        {"4294967295", -1},
        {"4294967296", 0},
        {"-4294967297", -1},
        {"-2147483649", 2147483647},
        {"18446744073709551617", 1},
        {"0b111111111111111111111111111111111", -1}};
    for (const auto& [text, expected] : cases) {
        INFO(text);
        REQUIRE(parse_integer(text) == expected);
    }
    Fixture f;
    f.files["scenario/RULES.TXT"] = "bad,0b11,0d-3,1; comment";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", std::nullopt));
    REQUIRE(reader.next_line());
    for (const auto expected : {0, 3, -3, 1, 0})
        REQUIRE(reader.next_integer() == expected);
}
TEST_CASE("F03 ability tokens use an independent unprefixed 16 bit grammar", "[classic][text]") {
    REQUIRE(parse_binary_flags("101x11") == 5);
    REQUIRE(parse_binary_flags("0b101") == 0);
    REQUIRE(parse_binary_flags("10000000000000001") == 1);
    REQUIRE(parse_binary_flags(std::string(100, '1')) == 65535);
    REQUIRE(parse_binary_flags("+1") == 0);
    REQUIRE(parse_binary_flags(" 1") == 0);
    Fixture f;
    f.files["scenario/RULES.TXT"] = " 101x11 , 0b101";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", std::nullopt));
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_binary_flags() == 5);
    REQUIRE(reader.next_binary_flags() == 0);
}
TEST_CASE("F03 failed reads retain cursor and backed line suffix without an open stream",
          "[classic][text]") {
    for (const auto status : {TextReadStatus::end, TextReadStatus::failure}) {
        Fixture f;
        f.files["scenario/RULES.TXT"] = "head,tail";
        auto reader = f.reader();
        REQUIRE(reader.open_low("rules", std::nullopt));
        REQUIRE(reader.next_line());
        REQUIRE(reader.next_token() == "head");
        auto expected = f.workspace.buffer;
        expected[0] = '\0';
        f.traces.at("scenario/RULES.TXT")->override_read = TextRead{status, {}};
        REQUIRE_FALSE(reader.next_line());
        REQUIRE(f.workspace.buffer == expected);
        REQUIRE(f.workspace.cursor == 5);
        REQUIRE(f.workspace.line().empty());
        REQUIRE(reader.next_token() == "tail");
        REQUIRE(reader.next_token().empty());
        REQUIRE(f.traces.at("scenario/RULES.TXT")->closes == 1);
        fault([&] { reader.next_line(); }, TextFault::unavailable_stream);
    }
}
TEST_CASE("F03 explicit opens apply extension and directory fallback with restoration",
          "[classic][text]") {
    Fixture f;
    f.files["home/RULES.TXT"] = "@UNITS\nrow";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", "units"));
    REQUIRE(f.opens == std::vector<std::string>{"scenario/RULES.TXT", "home/RULES.TXT"});
    REQUIRE(f.directories.working == "caller");
    REQUIRE(reader.source_path() == "home/RULES.TXT");
    REQUIRE(f.workspace.line() == "@UNITS");
    REQUIRE(f.workspace.cursor == 6);
    REQUIRE(reader.next_token().empty());
    REQUIRE(reader.next_line());
    REQUIRE(f.workspace.line() == "row");
    f.files["scenario/PACK.V2/RULES"] = "a";
    REQUIRE(reader.open_low("pack.v2/rules", std::nullopt));
    REQUIRE(f.opens.back() == "scenario/PACK.V2/RULES");
    REQUIRE(f.traces.at("home/RULES.TXT")->closes == 1);
    REQUIRE(f.workspace.line() == "row"); // A sectionless open does not read.
    REQUIRE(f.directories.search == "scenario");
    REQUIRE_FALSE(reader.open_low("missing", "A"));
    REQUIRE_FALSE(reader.is_open());
    REQUIRE(f.directories.working == "caller");
}
TEST_CASE("F03 wrapper retries missing scenario sections in the home file", "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = "@OTHER\nscenario";
    f.files["home/RULES.TXT"] = "@UNITS\nhome";
    auto reader = f.reader();
    REQUIRE(reader.open("rules", "units"));
    REQUIRE(f.opens == std::vector<std::string>{"scenario/RULES.TXT", "home/RULES.TXT"});
    REQUIRE(f.directories.working == "caller");
    REQUIRE(f.directories.search == "scenario");
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "home");
    REQUIRE(f.traces.at("scenario/RULES.TXT")->closes == 1);
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks.empty());
}
TEST_CASE("F03 reused streams search forward then rewind once without merging files",
          "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = "@A\nfirst\n@B\nsecond";
    f.files["home/RULES.TXT"] = "@MISSING\nnot merged";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", "B"));
    REQUIRE(reader.open_low(std::nullopt, "A"));
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks == std::vector<std::size_t>{0});
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "first");
    fault([&] { reader.open(std::nullopt, "MISSING"); }, TextFault::unavailable_stream);
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks == std::vector<std::size_t>{0, 0});
    REQUIRE(f.traces.at("scenario/RULES.TXT")->closes == 1);
    REQUIRE(f.opens == std::vector<std::string>{"scenario/RULES.TXT"});
    REQUIRE_FALSE(reader.is_open());
    REQUIRE(f.directories.working == "caller");
    REQUIRE(f.directories.search == "scenario");
}
TEST_CASE("F03 section matches use the whole trimmed line and preserve header cursor",
          "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] =
        "\xef\xbb\xbf@UNITS\n@UNITS ; comment\n@UNITS2\n \t@uNiTs \t\r\nvalue";
    // The BOM remains literal, so its byte length prevents a whole-line match.
    TextReader supplied(f.workspace, f.directories, f.services(), ascii_text_case());
    REQUIRE(supplied.open_low("rules", "UNITS"));
    REQUIRE(f.workspace.line() == "@uNiTs");
    REQUIRE(f.workspace.cursor == 6);
    REQUIRE(supplied.next_token().empty());
    REQUIRE(supplied.next_line());
    REQUIRE(f.workspace.line() == "value");
}
TEST_CASE("F03 duplicate section selection depends on cursor and supplied index offsets",
          "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = "@A\nfirst\n@A\nsecond\n";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", "A"));
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "first");
    REQUIRE(reader.open_low(std::nullopt, "A"));
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "second");
    reader.close();
    std::size_t offset = 9;
    auto services = f.services();
    services.section_offset = [&](std::string_view path, std::string_view section) {
        REQUIRE(path == "scenario/RULES.TXT");
        REQUIRE(section == "A");
        return std::optional<std::size_t>{offset};
    };
    TextReader indexed(f.workspace, f.directories, services, ascii_text_case());
    REQUIRE(indexed.open_low("rules", "A"));
    REQUIRE(indexed.next_line());
    REQUIRE(indexed.next_token() == "second");
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks == std::vector<std::size_t>{9});
    offset = 0;
    REQUIRE(indexed.open_low("rules", "A"));
    REQUIRE(indexed.next_line());
    REQUIRE(indexed.next_token() == "first");
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks == std::vector<std::size_t>{9});
    offset = 1000;
    fault([&] { indexed.open_low("rules", "A"); }, TextFault::seek_failed);
    REQUIRE(indexed.is_open());
    REQUIRE(f.workspace.line() == "first");
}
TEST_CASE("F03 read failures and exceptions do not become successful fallback content",
          "[classic][text]") {
    Fixture f;
    f.files["scenario/RULES.TXT"] = "@A\nfirst";
    auto reader = f.reader();
    REQUIRE(reader.open_low("rules", "A"));
    f.traces.at("scenario/RULES.TXT")->override_read = TextRead{TextReadStatus::failure, {}};
    REQUIRE_FALSE(reader.open_low(std::nullopt, "B"));
    REQUIRE(f.traces.at("scenario/RULES.TXT")->seeks.empty());
    REQUIRE_FALSE(reader.is_open());
    auto services = f.services();
    services.open = [&](std::string_view directory, std::string_view) -> OpenedText {
        REQUIRE(directory == "scenario");
        throw std::runtime_error("supplied open failure");
    };
    TextReader failing(f.workspace, f.directories, services, ascii_text_case());
    try {
        failing.open("rules", "A");
        FAIL("Expected the supplied open failure");
    } catch (const std::runtime_error& error) {
        REQUIRE(std::string(error.what()) == "supplied open failure");
    }
    REQUIRE(f.directories.working == "caller");
    REQUIRE(f.directories.search == "scenario");
}
TEST_CASE("F03 unsupported buffer and service reads stop at explicit boundaries",
          "[classic][text]") {
    Fixture f;
    auto reader = f.reader();
    fault([&] { reader.next_token(); }, TextFault::unbacked_cursor);
    f.workspace.cursor = 255;
    fault([&] { reader.next_token(); }, TextFault::unbacked_cursor);
    f.workspace.cursor = 0;
    f.workspace.buffer.fill('x');
    fault([&] { reader.next_token(); }, TextFault::unterminated_buffer);
    f.files["scenario/RULES.TXT"] = "ok\n";
    REQUIRE(reader.open_low("rules", std::nullopt));
    REQUIRE(reader.next_line());
    REQUIRE(f.workspace.backed_bytes == 3);
    f.workspace.cursor = 4;
    fault([&] { reader.next_token(); }, TextFault::unbacked_cursor);
    const auto saved = f.workspace.buffer;
    for (const auto& invalid :
         {TextRead{TextReadStatus::line, ""}, TextRead{TextReadStatus::line, std::string(255, 'x')},
          TextRead{TextReadStatus::line, "a\nb\n"}, TextRead{TextReadStatus::failure, "partial"}}) {
        f.traces.at("scenario/RULES.TXT")->override_read = invalid;
        fault([&] { reader.next_line(); }, TextFault::invalid_service_result);
        REQUIRE(f.workspace.buffer == saved);
        REQUIRE(f.workspace.cursor == 4);
    }
    for (const auto control : {'\0', '\x1a'}) {
        MemoryTextStream raw{std::string("ok\n") + control + "tail"};
        REQUIRE(raw.read().characters == "ok\n");
        fault([&] { raw.read(); }, TextFault::unsupported_control);
        REQUIRE(raw.position() == 3);
    }
}
TEST_CASE("F03 comparison service keeps unestablished byte folding explicit", "[classic][text]") {
    const auto comparison = ascii_text_case();
    REQUIRE(comparison.equal("@uNiTs", "@UNITS"));
    REQUIRE(comparison.equal("@pRoBe\xc9", "@Probe\xc9"));
    REQUIRE_FALSE(comparison.equal("name\xe9", "@UNITS"));
    REQUIRE_FALSE(comparison.equal("@A", "@AA"));
    fault([&] { comparison.equal("@Probe\xc9", "@probe\xe9"); }, TextFault::comparison_unavailable);
    fault([&] { comparison.uppercase_path("caf\xe9.txt"); }, TextFault::comparison_unavailable);
    Fixture f;
    f.files["scenario/CITY.TXT"] = "@probe\xe9\nName";
    TextReader reader(f.workspace, f.directories, f.services(), comparison);
    fault([&] { reader.open_low("CITY", "Probe\xc9"); }, TextFault::comparison_unavailable);
    REQUIRE(reader.is_open());
    REQUIRE(f.workspace.line() == "@probe\xe9");
    REQUIRE(f.workspace.cursor == 0);
    REQUIRE(f.directories.working == "caller");
}
TEST_CASE("F03 open requests remain owned across file service effects and retries",
          "[classic][text]") {
    Fixture f;
    std::string filename = "rules", section = "A";
    auto services = f.services();
    services.open = [&](std::string_view directory, std::string_view path) -> OpenedText {
        REQUIRE(path == "RULES.TXT");
        filename = "other";
        section = "B";
        const bool home = directory == "home";
        return {std::string(directory) + "/RULES.TXT",
                std::make_unique<MemoryTextStream>(home ? "@A\nselected" : "@OTHER\nignored")};
    };
    TextReader reader(f.workspace, f.directories, services, ascii_text_case());
    REQUIRE(reader.open(filename, section));
    REQUIRE(reader.source_path() == "home/RULES.TXT");
    REQUIRE(f.workspace.line() == "@A");
    REQUIRE(reader.next_line());
    REQUIRE(reader.next_token() == "selected");
}

TEST_CASE("Staged text source selection is explicit and a failed close is attempted once",
          "[classic][text]") {
    Fixture f;
    f.files["home/CITY.TXT"] = "@Tribe\nName\n@STOP\n";
    auto reader = f.reader();
    REQUIRE_FALSE(reader.open_source("scenario", "CITY"));
    REQUIRE(f.opens == std::vector<std::string>{"scenario/CITY.TXT"});
    REQUIRE(reader.open_source("home", "CITY"));
    REQUIRE(f.directories.working == "caller");
    REQUIRE(f.directories.search == "scenario");
    auto trace = f.traces.at("home/CITY.TXT");
    trace->fail_close = true;
    REQUIRE_THROWS_AS(reader.close(), std::runtime_error);
    REQUIRE_FALSE(reader.is_open());
    reader.close();
    REQUIRE(trace->closes == 1);
}
