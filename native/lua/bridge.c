#include "bridge.h"
#include "lauxlib.h"
#include "lua.h"
#include "lualib.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
    lua_State *main, *thread;
    size_t used, limit;
    const uint8_t *script, *reply;
    uint32_t script_length, reply_length, instructions, resumes, calls, remaining;
    int started, done, exhausted;
    char error[512];
} Context;

static void* allocate(void* userdata, void* pointer, size_t old_size, size_t size) {
    Context* c = (Context*)userdata;
    if (!pointer)
        old_size = 0;
    if (!size) {
        free(pointer);
        c->used -= old_size;
        return NULL;
    }
    if (size > old_size && size - old_size > c->limit - c->used)
        return NULL;
    void* result = realloc(pointer, size);
    if (result)
        c->used = c->used - old_size + size;
    return result;
}
static Context* context(lua_State* state) {
    return *(Context**)lua_getextraspace(state);
}
static void fail(Context* c, const char* text) {
    snprintf(c->error, sizeof(c->error), "%s", text ? text : "lua_error");
    c->done = -1;
}
static void budget(lua_State* state, lua_Debug* debug) {
    (void)debug;
    Context* c = context(state);
    if (c->remaining <= 1000) {
        c->exhausted = 1;
        lua_yield(state, 0);
    } else
        c->remaining -= 1000;
}
// Reply grammar: 0=nil, 1=false, 2=true, 3=i32 LE, 4=u32-length bytes,
// 5=u32-count array, 6=u32-count object (string keys). Decoder executes only
// inside the protected resumed continuation; allocation failures cannot unwind into CLR.
static uint32_t number(lua_State* state, Context* c, uint32_t* offset) {
    if (*offset > c->reply_length || c->reply_length - *offset < 4)
        luaL_error(state, "invalid_host_reply");
    const uint8_t* p = c->reply + *offset;
    *offset += 4;
    return (uint32_t)p[0] | ((uint32_t)p[1] << 8) | ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24);
}
static void value(lua_State* state, Context* c, uint32_t* offset, unsigned depth) {
    if (depth > 16 || *offset >= c->reply_length || !lua_checkstack(state, 4))
        luaL_error(state, "invalid_host_reply");
    uint8_t tag = c->reply[(*offset)++];
    switch (tag) {
    case 0:
        lua_pushnil(state);
        break;
    case 1:
    case 2:
        lua_pushboolean(state, tag == 2);
        break;
    case 3: {
        uint32_t n = number(state, c, offset);
        lua_pushinteger(state, n <= 0x7fffffffU ? (lua_Integer)n : (lua_Integer)n - 4294967296LL);
        break;
    }
    case 4: {
        uint32_t size = number(state, c, offset);
        if (size > c->reply_length - *offset)
            luaL_error(state, "invalid_host_reply");
        lua_pushlstring(state, (const char*)c->reply + *offset, size);
        *offset += size;
        break;
    }
    case 5:
    case 6: {
        uint32_t size = number(state, c, offset);
        if (size > 4096)
            luaL_error(state, "invalid_host_reply");
        lua_createtable(state, tag == 5 ? (int)size : 0, tag == 6 ? (int)size : 0);
        for (uint32_t i = 0; i < size; ++i) {
            if (tag == 6) {
                if (*offset >= c->reply_length || c->reply[*offset] != 4)
                    luaL_error(state, "invalid_host_reply");
                value(state, c, offset, depth + 1);
                value(state, c, offset, depth + 1);
                lua_rawset(state, -3);
            } else {
                value(state, c, offset, depth + 1);
                lua_rawseti(state, -2, (lua_Integer)i + 1);
            }
        }
        break;
    }
    default:
        luaL_error(state, "invalid_host_reply");
    }
}
static int reply(lua_State* state, int status, lua_KContext ignored) {
    (void)status;
    (void)ignored;
    Context* c = context(state);
    uint32_t offset = 0;
    value(state, c, &offset, 0);
    if (offset != c->reply_length)
        return luaL_error(state, "invalid_host_reply");
    return 1;
}
static int host_call(lua_State* state) {
    if (lua_gettop(state) != 2)
        return luaL_error(state, "host_argument_count");
    for (int i = 1; i <= 2; ++i) {
        luaL_checktype(state, i, LUA_TSTRING);
        size_t length;
        lua_tolstring(state, i, &length);
        if (length > 65536)
            return luaL_error(state, "host_argument_budget");
    }
    return lua_yieldk(state, 2, 0, reply);
}
// Empty-string repetition can perform arbitrarily many native iterations without
// allocating. Bound its count before entering the stock library implementation.
static int bounded_repeat(lua_State* state) {
    if (luaL_checkinteger(state, 2) > 65536)
        return luaL_error(state, "native_work_budget");
    int arguments = lua_gettop(state);
    lua_pushvalue(state, lua_upvalueindex(1));
    lua_insert(state, 1);
    lua_call(state, arguments, 1);
    return 1;
}
static int initialize(lua_State* state) {
    Context* c = context(state);
    luaL_requiref(state, "_G", luaopen_base, 1);
    lua_pop(state, 1);
    luaL_requiref(state, "table", luaopen_table, 1);
    lua_pop(state, 1);
    luaL_requiref(state, "string", luaopen_string, 1);
    lua_pop(state, 1);
    luaL_requiref(state, "math", luaopen_math, 1);
    lua_pop(state, 1);
    luaL_requiref(state, "utf8", luaopen_utf8, 1);
    lua_pop(state, 1);
    // No unmanaged resource acquisition, arbitrary loaders, metatable finalizers,
    // unyieldable error handlers, child coroutines or unbounded pattern/callback work.
    const char* blocked[] = {"dofile",         "loadfile", "load",
                             "collectgarbage", "print",    "getmetatable",
                             "setmetatable",   "xpcall",   NULL};
    for (int i = 0; blocked[i]; ++i) {
        lua_pushnil(state);
        lua_setglobal(state, blocked[i]);
    }
    lua_getglobal(state, "string");
    const char* patterns[] = {"find", "match", "gmatch", "gsub", "dump", NULL};
    for (int i = 0; patterns[i]; ++i) {
        lua_pushnil(state);
        lua_setfield(state, -2, patterns[i]);
    }
    lua_getfield(state, -1, "rep");
    lua_pushcclosure(state, bounded_repeat, 1);
    lua_setfield(state, -2, "rep");
    lua_pop(state, 1);
    lua_getglobal(state, "table");
    lua_pushnil(state);
    lua_setfield(state, -2, "sort");
    lua_pushnil(state);
    lua_setfield(state, -2, "move");
    lua_pop(state, 1);
    lua_getglobal(state, "math");
    lua_pushnil(state);
    lua_setfield(state, -2, "random");
    lua_pushnil(state);
    lua_setfield(state, -2, "randomseed");
    lua_pop(state, 1);
    lua_pushcfunction(state, host_call);
    lua_setglobal(state, "_oc2_call");
    c->thread = lua_newthread(state); // Main stack roots it until explicit close.
    *(Context**)lua_getextraspace(c->thread) = c;
    if (luaL_loadbufferx(c->thread, (const char*)c->script, c->script_length, "automation", "t") !=
        LUA_OK)
        fail(c, lua_type(c->thread, -1) == LUA_TSTRING ? lua_tostring(c->thread, -1) : "lua_error");
    lua_sethook(c->thread, budget, LUA_MASKCOUNT, 1000);
    return 1;
}
uint32_t oc2_lua_abi(void) {
    return 1;
}
const char* oc2_lua_version(void) {
    return LUA_RELEASE;
}
void* oc2_lua_create(const uint8_t* script, uint32_t length, uint32_t memory, uint32_t instructions,
                     uint32_t resumes) {
    Context* c = (Context*)calloc(1, sizeof(Context));
    if (!c)
        return NULL;
    if (!script || length > 131072 || memory < 32768 || memory > 16777216 || instructions < 1000 ||
        instructions > 1000000 || !resumes || resumes > 10000) {
        fail(c, "invalid_limits");
        return c;
    }
    c->limit = memory;
    // Hook counts are in 1000-instruction quanta; round down, never exceed the limit.
    c->instructions = instructions - instructions % 1000;
    c->resumes = resumes;
    c->script = script;
    c->script_length = length;
    c->main = lua_newstate(allocate, c, 0);
    if (!c->main) {
        fail(c, "memory_budget");
        return c;
    }
    *(Context**)lua_getextraspace(c->main) = c;
    lua_pushcfunction(c->main, initialize);
    if (lua_pcall(c->main, 0, 1, 0) != LUA_OK)
        fail(c, lua_type(c->main, -1) == LUA_TSTRING ? lua_tostring(c->main, -1) : "lua_error");
    c->script = NULL;
    return c;
}
int oc2_lua_step(void* pointer, const uint8_t* data, uint32_t length) {
    Context* c = (Context*)pointer;
    if (!c || c->done < 0)
        return -1;
    if (c->done > 0)
        return 0;
    if (++c->calls > c->resumes) {
        fail(c, "host_call_budget");
        return -1;
    }
    if (length > 65536 || (length && !data)) {
        fail(c, "host_reply_budget");
        return -1;
    }
    if (c->started)
        lua_settop(c->thread, 0);
    c->started = 1;
    c->remaining = c->instructions;
    c->reply = data;
    c->reply_length = length;
    int results = 0, status = lua_resume(c->thread, NULL, 0, &results);
    c->reply = NULL;
    if (c->exhausted) {
        fail(c, "instruction_budget");
        return -1;
    }
    if (status == LUA_YIELD)
        return 1;
    if (status == LUA_OK) {
        c->done = 1;
        return 0;
    }
    fail(c, lua_type(c->thread, -1) == LUA_TSTRING ? lua_tostring(c->thread, -1) : "lua_error");
    return -1;
}
const uint8_t* oc2_lua_argument(void* pointer, uint32_t index, uint32_t* length) {
    Context* c = (Context*)pointer;
    *length = 0;
    if (!c || !c->thread || index > 1 || c->done ||
        lua_type(c->thread, (int)index + 1) != LUA_TSTRING)
        return NULL;
    size_t size;
    const char* result = lua_tolstring(c->thread, (int)index + 1, &size);
    if (size > 65536)
        return NULL;
    *length = (uint32_t)size;
    return (const uint8_t*)result;
}
const char* oc2_lua_error(void* pointer) {
    return pointer ? ((Context*)pointer)->error : "allocation_failed";
}
uint32_t oc2_lua_memory(void* pointer) {
    return pointer ? (uint32_t)((Context*)pointer)->used : 0;
}
void oc2_lua_destroy(void* pointer) {
    Context* c = (Context*)pointer;
    if (!c)
        return;
    if (c->main)
        lua_close(c->main);
    free(c);
}
