// Authored bridge ABI/resource tests. No classic-game implementation or fixture inputs.
#include "bridge.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static void* create(const char* script, uint32_t memory, uint32_t instructions, uint32_t resumes) {
    void* c = oc2_lua_create((const uint8_t*)script, (uint32_t)strlen(script), memory, instructions,
                             resumes);
    assert(c);
    return c;
}
static void failure(const char* script, const char* expected, uint32_t memory) {
    void* c = create(script, memory, 10000, 10);
    assert(oc2_lua_step(c, NULL, 0) == -1);
    assert(strstr(oc2_lua_error(c), expected));
    assert(oc2_lua_step(c, NULL, 0) == -1);
    assert(oc2_lua_memory(c) <= memory);
    oc2_lua_destroy(c);
}
int main(void) {
    assert(oc2_lua_abi() == 1);
    assert(strcmp(oc2_lua_version(), "Lua 5.5.1") == 0);
    failure("while true do end", "instruction_budget", 262144);
    failure("pcall(function() while true do end end); while true do end", "instruction_budget",
            262144);
    failure("string.rep('', 9223372036854775807)", "native_work_budget", 262144);
    failure("table.move({}, 1, 9223372036854775807, 1)", "nil value", 262144);
    failure("error(12345)", "lua_error", 262144);
    failure("error({})", "lua_error", 262144);
    failure("this is not lua", "syntax", 262144);
    failure("local x=string.rep('0123456789abcdef', 65536)", "memory", 65536);
    failure("return 1", "invalid_limits", 1);
    failure("_oc2_call('op', {})", "string expected", 262144);
    void* c =
        create("assert(io==nil and os==nil and package==nil and debug==nil and coroutine==nil); "
               "assert(load==nil and setmetatable==nil and math.random==nil and "
               "string.match==nil); local x=_oc2_call('units',''); assert(x.a[1]==-2147483648 and "
               "x.a[2]==true and x.a[3]=='hi'); _oc2_call('ok','done')",
               262144, 10000, 10);
    assert(oc2_lua_step(c, NULL, 0) == 1);
    uint32_t length;
    const uint8_t* argument = oc2_lua_argument(c, 0, &length);
    assert(length == 5 && memcmp(argument, "units", length) == 0);
    const uint8_t data[] = {6, 1, 0, 0, 0, 4,   1, 0, 0, 0, 'a', 5, 3,   0,  0,
                            0, 3, 0, 0, 0, 128, 2, 4, 2, 0, 0,   0, 'h', 'i'};
    assert(oc2_lua_step(c, data, sizeof(data)) == 1);
    argument = oc2_lua_argument(c, 1, &length);
    assert(length == 4 && memcmp(argument, "done", length) == 0);
    const uint8_t nil[] = {0};
    assert(oc2_lua_step(c, nil, 1) == 0);
    assert(oc2_lua_step(c, nil, 1) == 0);
    oc2_lua_destroy(c);
    // Every prefix of a valid nontrivial typed response is rejected while protected.
    for (uint32_t size = 0; size < sizeof(data); ++size) {
        c = create("local x=_oc2_call('op','')", 262144, 10000, 10);
        assert(oc2_lua_step(c, NULL, 0) == 1);
        assert(oc2_lua_step(c, data, size) == -1);
        assert(strstr(oc2_lua_error(c), "invalid_host_reply"));
        oc2_lua_destroy(c);
    }
    c = create("while true do _oc2_call('wait','') end", 262144, 10000, 2);
    assert(oc2_lua_step(c, NULL, 0) == 1);
    assert(oc2_lua_step(c, nil, 1) == 1);
    assert(oc2_lua_step(c, nil, 1) == -1);
    assert(strcmp(oc2_lua_error(c), "host_call_budget") == 0);
    oc2_lua_destroy(c);
    // Repeated GC activity across yields exercises the native coroutine root.
    c = create("for i=1,200 do local a={} for j=1,200 do a[j]=tostring(j)..tostring(i) end "
               "_oc2_call('wait','') end",
               262144, 10000, 201);
    for (int i = 0; i < 200; ++i)
        assert(oc2_lua_step(c, nil, 1) == 1);
    assert(oc2_lua_step(c, nil, 1) == 0);
    oc2_lua_destroy(c);
    // Reply allocation errors are contained, including Lua initialization pressure.
    uint8_t huge[60005] = {4, 96, 234, 0, 0};
    memset(huge + 5, 'a', 60000);
    for (uint32_t memory = 32768; memory < 65536; memory += 1024) {
        c = create("local x=_oc2_call('op','')", memory, 10000, 10);
        if (oc2_lua_step(c, NULL, 0) == 1)
            assert(oc2_lua_step(c, huge, sizeof(huge)) == -1);
        assert(oc2_lua_memory(c) <= memory);
        oc2_lua_destroy(c);
    }
    oc2_lua_destroy(NULL);
    puts("Lua bridge ABI, sandbox, budget, reply, GC and disposal checks passed.");
    return 0;
}
