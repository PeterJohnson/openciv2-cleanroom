-- API 1: owned observations and descriptor commands through a scoped player client.
local call = _oc2_call
_oc2_call = nil
game = {}
function game.units() return call('units', '') end
function game.production_events() return call('production_events', '') end
function game.production_choices(city) return call('production_choices', tostring(city.slot) .. ':' .. city.token) end
function game.commands() return call('commands', '') end
function game.command(text) return call('command', text) end
function game.result(id) return call('result', id) end
function game.reply(id, choice, text)
    if text == nil then return call('reply', id .. ':' .. tostring(choice)) end
    assert(type(text) == 'string', 'reply text must be a string')
    return call('reply_text', id .. ':' .. tostring(choice) .. ':' .. text)
end
function game.cancel(id) return call('cancel', id) end
function game.wait() return call('wait', '') end
function game.log(text) return call('log', text) end
