export module openciv.session.client_connection;
import std;
export import openciv.session.client_types;
export import openciv.query.live_records;

export namespace openciv::session::detail {
// Runtime-owned capability behind a copyable client handle. Implementations must
// validate their session and lease on every operation; no engine state crosses it.
class ClientConnection {
public:
    ClientConnection() = default;
    virtual ~ClientConnection() = default;
    ClientConnection(const ClientConnection&) = delete;
    ClientConnection& operator=(const ClientConnection&) = delete;
    virtual ClientIdentity identity() const = 0;
    virtual query::LiveRecords<query::OwnUnit> own_units() const = 0;
    virtual query::LiveRecords<query::OwnCity> own_cities() const = 0;
    virtual std::vector<CommandDescriptor> commands() const = 0;
    virtual CommandUpdate submit(CommandRequest request) const = 0;
    virtual std::vector<CommandUpdate> updates() const = 0;
    virtual std::optional<CommandUpdate> result(CommandId command) const = 0;
    virtual InputReceipt reply(InteractionId interaction, std::int32_t choice) const = 0;
    virtual InputReceipt cancel(CommandId command) const = 0;

protected:
    // Dependent construction avoids a reverse import of the public handle.
    template <class Client> static Client issue(std::shared_ptr<ClientConnection> connection) {
        return Client{std::move(connection)};
    }
};
} // namespace openciv::session::detail
