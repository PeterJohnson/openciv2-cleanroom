import std;
import openciv.network.messages;
import openciv.network.stream;

// Read-only checker for independently captured N05 wire records. This is not a
// socket backend, original-program driver, or N01 application admission check.

int main(int argc, char** argv) {
    using namespace openciv;
    try {
        const bool datagram = argc == 3 && std::string_view{argv[1]} == "--datagram";
        if (argc != 2 && !datagram)
            throw std::invalid_argument("Usage: oc2_wire_roundtrip [--datagram] INPUT.bin");
        std::ifstream input(argv[datagram ? 2 : 1], std::ios::binary);
        if (!input)
            throw std::runtime_error("Cannot open wire input");
        const formats::Bytes bytes{std::istreambuf_iterator<char>{input}, {}};
        if (input.bad() || bytes.empty())
            throw std::runtime_error("Empty or unreadable wire input");
        if (datagram) {
            const auto packet = network::decode_datagram(bytes);
            const auto message = network::decode_message(packet.payload);
            const auto payload = network::encode_message(message);
            const std::span<const formats::Byte> view{payload};
            formats::Bytes backing(bytes.size());
            if (payload.size() > 512 ||
                network::encode_datagram(packet.header, static_cast<std::uint32_t>(payload.size()),
                                         &view, backing) != bytes)
                throw std::runtime_error(
                    "Datagram reconstruction differs or exceeds nominal capacity");
            std::cout << "{\"bytes\":" << bytes.size() << ",\"kind\":" << message.header.kind
                      << ",\"byteEqual\":true}\n";
            return 0;
        }
        std::size_t frames = 0, messages = 0;
        for (const auto chunk : {std::size_t{1}, bytes.size()}) {
            network::FrameReceiver receiver;
            network::ReceiveCallbacks callbacks;
            formats::Bytes encoded;
            frames = messages = 0;
            auto verify_frame = [&](network::FrameView frame) {
                const auto wire = network::encode_frame(frame.tag, frame.payload, frame.padding);
                encoded.insert(encoded.end(), wire.begin(), wire.end());
                ++frames;
                if (frame.payload.size() >= 4 &&
                    formats::ByteReader{frame.payload}.u32() == network::message_magic) {
                    if (network::encode_message(network::decode_message(frame.payload)) !=
                        formats::Bytes{frame.payload.begin(), frame.payload.end()})
                        throw std::runtime_error("Application message bytes differ");
                    ++messages;
                }
            };
            callbacks.frame = verify_frame;
            for (std::size_t offset = 0; offset < bytes.size(); offset += chunk) {
                const auto part =
                    std::span{bytes}.subspan(offset, std::min(chunk, bytes.size() - offset));
                if (receiver.feed(part, callbacks).stop != network::ReceiveStop::exhausted)
                    throw std::runtime_error("Wire receiver rejected input");
            }
            if (receiver.incomplete() || encoded != bytes)
                throw std::runtime_error("Incomplete or differing reconstructed stream");
        }
        std::cout << "{\"frames\":" << frames << ",\"applicationMessages\":" << messages
                  << ",\"bytes\":" << bytes.size()
                  << ",\"oneByteAndCoalesced\":true,\"byteEqual\":true}\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
