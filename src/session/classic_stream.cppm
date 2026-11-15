export module openciv.session.classic_stream;

import std;
export import openciv.formats.classic_io;

export extern "C++" {
    namespace openciv::session {
    // A uniquely owned open file. Explicit close reports finalization failure;
    // repeated close never repeats I/O, even when the first close threw. Derived
    // destructors call close_on_destruction() while their resources still exist.
    // Destruction releases resources without throwing or replacing an earlier error.
    class CoreFile {
    public:
        virtual ~CoreFile() = default;
        CoreFile(const CoreFile&) = delete;
        CoreFile& operator=(const CoreFile&) = delete;
        bool close() {
            if (!closed_) {
                closed_ = true;
                finalized_ = finish();
            }
            return finalized_;
        }

    protected:
        CoreFile() = default;
        void require_open() const {
            if (closed_)
                throw std::logic_error("Classic file used after close");
        }
        void close_on_destruction() noexcept {
            try {
                (void)close();
            } catch (...) {
            }
        }

    private:
        virtual bool finish() = 0;
        bool closed_ = false, finalized_ = false;
    };
    class CoreInput : public CoreFile {
    public:
        formats::ReadResult item(std::size_t size) {
            require_open();
            return read_item(size);
        }
        std::optional<formats::Byte> byte() {
            require_open();
            return read_byte();
        }

    private:
        virtual formats::ReadResult read_item(std::size_t size) = 0;
        virtual std::optional<formats::Byte> read_byte() = 0;
    };
    class CoreOutput : public CoreFile {
    public:
        bool item(std::span<const formats::Byte> bytes) {
            require_open();
            return write_item(bytes);
        }

    private:
        virtual bool write_item(std::span<const formats::Byte> bytes) = 0;
    };
    } // namespace openciv::session
}
