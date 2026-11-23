-- After one own-city completion, choose the first offered item different
-- from the one just completed, then wait for the command's result.
while true do
    local batch = game.production_events()
    assert(batch.dropped == 0, 'production event backlog overflowed')
    for _, event in ipairs(batch.events) do
        local choices = game.production_choices(event.city)
        while choices and not choices.ready do
            game.wait()
            choices = game.production_choices(event.city)
        end
        if choices then
            for _, option in ipairs(choices.options) do
                if option.id ~= event.item then
                    local request = 'city.production city=' .. event.city.slot .. ':' .. event.city.token
                        .. ' type=' .. option.id
                    local command = game.command(request)
                    while command.status == 'Pending' do
                        game.wait()
                        command = game.result(command.id)
                    end
                    assert(command.status == 'Completed', command.code)
                    game.log('Selected ' .. option.name)
                    return
                end
            end
        end
    end
    game.wait()
end
