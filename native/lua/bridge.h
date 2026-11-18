#pragma once
#include <stdint.h>
#ifdef _WIN32
#define OC2_LUA_API __declspec(dllexport)
#else
#define OC2_LUA_API __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif
// ABI 1. No game state, CLR pointers, managed callbacks or object layouts cross this boundary.
OC2_LUA_API uint32_t oc2_lua_abi(void);
OC2_LUA_API const char* oc2_lua_version(void);
OC2_LUA_API void* oc2_lua_create(const uint8_t* script, uint32_t length, uint32_t memory,
                                 uint32_t instructions, uint32_t resumes);
// -1 failure, 0 completed, 1 yielded op/argument. Reply is bounded typed data, never source code.
OC2_LUA_API int oc2_lua_step(void* context, const uint8_t* reply, uint32_t length);
OC2_LUA_API const uint8_t* oc2_lua_argument(void* context, uint32_t index, uint32_t* length);
OC2_LUA_API const char* oc2_lua_error(void* context);
OC2_LUA_API uint32_t oc2_lua_memory(void* context);
OC2_LUA_API void oc2_lua_destroy(void* context);
#ifdef __cplusplus
}
#endif
