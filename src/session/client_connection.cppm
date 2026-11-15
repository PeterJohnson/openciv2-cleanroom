export module openciv.session.client_connection;
import std;
export import openciv.session.client_types;
export import openciv.query.live_records;

export namespace openciv::session::detail {
/// Runtime-owned capability behind a copyable client handle. Implementations must
/// validate their session and lease on every operation; no engine state crosses it.
class ClientConnection {
public:
    ClientConnection() = default;
    virtual ~ClientConnection() = default;
    ClientConnection(const ClientConnection&) = delete;
    ClientConnection& operator=(const ClientConnection&) = delete;
    /// Return the validated identity of this client lease.
    virtual ClientIdentity identity() const = 0;
    /// Return an owned snapshot of units visible through this participant binding.
    virtual query::LiveRecords<query::OwnUnit> own_units() const = 0;
    /// Return an owned snapshot of cities visible through this participant binding.
    virtual query::LiveRecords<query::OwnCity> own_cities() const = 0;
    /// List commands discoverable with this binding access level.
    virtual std::vector<CommandDescriptor> commands() const = 0;
    /// Validate and admit a semantic command request, returning its initial status.
    virtual CommandUpdate submit(CommandRequest request) const = 0;
    /// Drain queued command notifications for this binding.
    virtual std::vector<CommandUpdate> updates() const = 0;
    /// Inspect a retained command result when accessible to this binding.
    virtual std::optional<CommandUpdate> result(CommandId command) const = 0;
    /// Submit a choice for a pending interaction using this client lease.
    virtual InputReceipt reply(InteractionId interaction, std::int32_t choice) const = 0;
    /// Request cancellation of an accessible command using this client lease.
    virtual InputReceipt cancel(CommandId command) const = 0;

protected:
    /// Dependent construction avoids a reverse import of the public handle.
    template <class Client> static Client issue(std::shared_ptr<ClientConnection> connection) {
        return Client{std::move(connection)};
    }
};
} // namespace openciv::session::detail
