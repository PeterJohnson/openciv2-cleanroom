export module openciv.io.stream;

import std;
export import openciv.formats.bytes;

export namespace openciv::io {
/// A transfer can return a short prefix; the stream owns its cursor and I/O effects.
struct ReadResult {
    formats::Bytes prefix;
    bool complete = false;
};
/// A uniquely owned open file. Explicit close reports finalization failure;
/// repeated close never repeats I/O, even when the first close threw. Derived
/// destructors call close_on_destruction() while their resources still exist.
/// Destruction releases resources without throwing or replacing an earlier error.
class CoreFile {
public:
    virtual ~CoreFile() = default;
    CoreFile(const CoreFile&) = delete;
    CoreFile& operator=(const CoreFile&) = delete;
    /// Close once and return the retained completion result on subsequent calls.
    bool close() {
        if (!closed_) {
            closed_ = true;
            finalized_ = finish();
        }
        return finalized_;
    }

protected:
    CoreFile() = default;
    /// Reject an operation after the stream has been closed.
    void require_open() const {
        if (closed_)
            throw std::logic_error("File used after close");
    }
    /// Attempt cleanup during destruction without allowing exceptions to escape.
    void close_on_destruction() noexcept {
        try {
            (void)close();
        } catch (...) {
        }
    }

private:
    /// Finish the underlying stream and report its close result.
    virtual bool finish() = 0;
    bool closed_ = false, finalized_ = false;
};
/// An item-oriented input stream with explicit short-read and byte-read results.
class CoreInput : public CoreFile {
public:
    /// Read an item of the requested extent after checking the stream is open.
    ReadResult item(std::size_t size) {
        require_open();
        return read_item(size);
    }
    /// Read one byte, returning nullopt when no byte is available.
    std::optional<formats::Byte> byte() {
        require_open();
        return read_byte();
    }

private:
    /// Implement one item read while preserving its short-read status.
    virtual ReadResult read_item(std::size_t size) = 0;
    /// Implement one byte read from the backing stream.
    virtual std::optional<formats::Byte> read_byte() = 0;
};
/// An item-oriented output stream with explicit write and close results.
class CoreOutput : public CoreFile {
public:
    /// Write an item after checking the stream is open.
    bool item(std::span<const formats::Byte> bytes) {
        require_open();
        return write_item(bytes);
    }

private:
    /// Implement one complete item write and report success.
    virtual bool write_item(std::span<const formats::Byte> bytes) = 0;
};
} // namespace openciv::io
