-- API 1: owned observations and descriptor commands through a scoped player client.
local call = _oc2_call
_oc2_call = nil
game = {}
function game.units() return call('units', '') end
function game.commands() return call('commands', '') end
function game.command(text) return call('command', text) end
function game.result(id) return call('result', id) end
function game.reply(id, choice) return call('reply', id .. ':' .. tostring(choice)) end
function game.cancel(id) return call('cancel', id) end
function game.wait() return call('wait', '') end
function game.log(text) return call('log', text) end
