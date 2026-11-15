export module openciv.tests.classic_stream_fixture;

import std;
export import openciv.session.classic_stream;
export import openciv.tests.service_fixture;

export extern "C++" {
    namespace openciv::test {
    // Local synchronous test composition. Captures must outlive the returned file;
    // required bindings are validated before a file can be handed to production.
    struct InputCallbacks {
        formats::ReadItem item;
        std::function<std::optional<formats::Byte>()> byte;
        std::function<bool()> close;
    };
    struct OutputCallbacks {
        formats::WriteItem item;
        std::function<bool()> close;
    };
    class InputFile final : public session::CoreInput {
    public:
        explicit InputFile(InputCallbacks callbacks) : callbacks_(std::move(callbacks)) {
            if (!callbacks_.item || !callbacks_.close)
                throw std::invalid_argument("Test input requires read and close bindings");
        }
        ~InputFile() override { close_on_destruction(); }

    private:
        formats::ReadResult read_item(std::size_t size) override { return callbacks_.item(size); }
        std::optional<formats::Byte> read_byte() override {
            if (callbacks_.byte)
                return callbacks_.byte();
            const auto result = callbacks_.item(1);
            return result.prefix.empty() ? std::nullopt : std::optional{result.prefix.front()};
        }
        bool finish() override { return callbacks_.close(); }
        InputCallbacks callbacks_;
    };
    class OutputFile final : public session::CoreOutput {
    public:
        explicit OutputFile(OutputCallbacks callbacks) : callbacks_(std::move(callbacks)) {
            if (!callbacks_.item || !callbacks_.close)
                throw std::invalid_argument("Test output requires write and close bindings");
        }
        ~OutputFile() override { close_on_destruction(); }

    private:
        bool write_item(std::span<const formats::Byte> bytes) override {
            return callbacks_.item(bytes);
        }
        bool finish() override { return callbacks_.close(); }
        OutputCallbacks callbacks_;
    };
    inline std::unique_ptr<session::CoreInput> input_file(InputCallbacks callbacks) {
        return std::make_unique<InputFile>(std::move(callbacks));
    }
    inline std::unique_ptr<session::CoreOutput> output_file(OutputCallbacks callbacks) {
        return std::make_unique<OutputFile>(std::move(callbacks));
    }
    } // namespace openciv::test
}
