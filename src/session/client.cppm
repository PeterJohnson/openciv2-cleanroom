export module openciv.session.client;
import std;
export import openciv.session.client_types;
export import openciv.query.live_records;
import openciv.session.client_connection;

export namespace openciv::session {
// The only surface handed to clients. Copying shares the same lease; no public
// constructor can manufacture or alter authority. Owned query results may be
// retained, but a revoked client can obtain no subsequent observation.
class PlayerClient {
public:
    PlayerClient(const PlayerClient&) = default;
    PlayerClient& operator=(const PlayerClient&) = default;
    ClientIdentity identity() const;
    query::LiveRecords<query::OwnUnit> own_units() const;
    query::LiveRecords<query::OwnCity> own_cities() const;
    std::vector<CommandDescriptor> commands() const;
    CommandUpdate submit(CommandRequest request) const;
    // Ordered notifications contain only effects not previously delivered to
    // this lease. result() remains a cumulative snapshot until owner retirement.
    std::vector<CommandUpdate> updates() const;
    std::optional<CommandUpdate> result(CommandId command) const;
    InputReceipt reply(InteractionId interaction, std::int32_t choice) const;
    InputReceipt cancel(CommandId command) const;

private:
    friend class detail::ClientConnection;
    explicit PlayerClient(std::shared_ptr<detail::ClientConnection> connection);
    std::shared_ptr<detail::ClientConnection> connection_;
};
} // namespace openciv::session

module :private;
namespace openciv::session {
PlayerClient::PlayerClient(std::shared_ptr<detail::ClientConnection> connection)
    : connection_(std::move(connection)) {}
ClientIdentity PlayerClient::identity() const {
    return connection_->identity();
}
query::LiveRecords<query::OwnUnit> PlayerClient::own_units() const {
    return connection_->own_units();
}
query::LiveRecords<query::OwnCity> PlayerClient::own_cities() const {
    return connection_->own_cities();
}
std::vector<CommandDescriptor> PlayerClient::commands() const {
    return connection_->commands();
}
CommandUpdate PlayerClient::submit(CommandRequest request) const {
    return connection_->submit(std::move(request));
}
std::vector<CommandUpdate> PlayerClient::updates() const {
    return connection_->updates();
}
std::optional<CommandUpdate> PlayerClient::result(CommandId command) const {
    return connection_->result(command);
}
InputReceipt PlayerClient::reply(InteractionId interaction, std::int32_t choice) const {
    return connection_->reply(interaction, choice);
}
InputReceipt PlayerClient::cancel(CommandId command) const {
    return connection_->cancel(command);
}
} // namespace openciv::session
