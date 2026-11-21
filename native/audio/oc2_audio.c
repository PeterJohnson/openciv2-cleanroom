/* P10 output bridge. An opaque ABI keeps miniaudio layouts/callbacks out of managed code. */
#define MINIAUDIO_IMPLEMENTATION
#define MA_NO_RESOURCE_MANAGER
#define MA_NO_DECODING
#define MA_NO_ENCODING
#include "miniaudio.h"
#include <stdlib.h>
#include <math.h>

#ifdef _WIN32
#define OC2_API __declspec(dllexport)
#else
#define OC2_API __attribute__((visibility("default")))
#endif

typedef struct {
    ma_context context;
    ma_engine engine;
    ma_audio_buffer buffer;
    ma_sound sound;
    int context_ready, engine_ready, buffer_ready, sound_ready;
} oc2_audio;

OC2_API int oc2_audio_version(void) { return MA_VERSION_MAJOR * 1000000 + MA_VERSION_MINOR * 1000 + MA_VERSION_REVISION; }
OC2_API void oc2_audio_close(oc2_audio* audio)
{
    if (!audio) return;
    /* Stop and join device processing before releasing its graph and sample memory. */
    if (audio->engine_ready) ma_engine_stop(&audio->engine);
    if (audio->sound_ready) ma_sound_uninit(&audio->sound);
    if (audio->engine_ready) ma_engine_uninit(&audio->engine);
    if (audio->buffer_ready) ma_audio_buffer_uninit(&audio->buffer);
    if (audio->context_ready) ma_context_uninit(&audio->context);
    free(audio);
}

static int open_audio(const float* samples, int frames, int rate, int channels, int loop, int offline, oc2_audio** result)
{
    ma_result status;
    ma_engine_config engine_config;
    ma_audio_buffer_config buffer_config;
    oc2_audio* audio;
    if (!result) return MA_INVALID_ARGS;
    *result = NULL;
    if (!samples || frames <= 0 || rate < 8000 || rate > 192000 || channels < 1 || channels > 2 ||
        frames > 16 * 1024 * 1024 / channels || (ma_uint64)frames > (ma_uint64)rate * 600) return MA_INVALID_ARGS;
    for (int i = 0; i < frames * channels; ++i)
        if (!isfinite(samples[i]) || samples[i] < -1 || samples[i] > 1) return MA_INVALID_ARGS;
    audio = (oc2_audio*)calloc(1, sizeof(*audio));
    if (!audio) return MA_OUT_OF_MEMORY;
    engine_config = ma_engine_config_init();
    engine_config.channels = channels;
    engine_config.sampleRate = rate;
    engine_config.noAutoStart = MA_TRUE;
    engine_config.noDevice = offline;
    if (!offline) {
        /* Explicit lists exclude the null backend: missing output must fail honestly. */
#if defined(_WIN32)
        ma_backend backends[] = { ma_backend_wasapi, ma_backend_dsound, ma_backend_winmm };
#elif defined(__APPLE__)
        ma_backend backends[] = { ma_backend_coreaudio };
#else
        ma_backend backends[] = { ma_backend_pulseaudio, ma_backend_alsa, ma_backend_jack };
#endif
        status = ma_context_init(backends, sizeof(backends) / sizeof(backends[0]), NULL, &audio->context);
        if (status != MA_SUCCESS) goto failed;
        audio->context_ready = 1;
        engine_config.pContext = &audio->context;
    }
    status = ma_engine_init(&engine_config, &audio->engine);
    if (status != MA_SUCCESS) goto failed;
    audio->engine_ready = 1;
    buffer_config = ma_audio_buffer_config_init(ma_format_f32, channels, frames, samples, NULL);
    buffer_config.sampleRate = rate;
    status = ma_audio_buffer_init_copy(&buffer_config, &audio->buffer);
    if (status != MA_SUCCESS) goto failed;
    audio->buffer_ready = 1;
    status = ma_sound_init_from_data_source(&audio->engine, &audio->buffer,
        MA_SOUND_FLAG_NO_SPATIALIZATION | MA_SOUND_FLAG_NO_PITCH, NULL, &audio->sound);
    if (status != MA_SUCCESS) goto failed;
    audio->sound_ready = 1;
    ma_sound_set_looping(&audio->sound, loop != 0);
    status = ma_sound_start(&audio->sound);
    if (status != MA_SUCCESS) goto failed;
    if (!offline) {
        status = ma_engine_start(&audio->engine);
        if (status != MA_SUCCESS) goto failed;
    }
    *result = audio;
    return MA_SUCCESS;
failed:
    oc2_audio_close(audio);
    return status;
}

OC2_API int oc2_audio_open(const float* samples, int frames, int rate, int channels, int loop, oc2_audio** result)
{ return open_audio(samples, frames, rate, channels, loop, 0, result); }
OC2_API int oc2_audio_state(oc2_audio* audio)
{
    if (!audio) return -1;
    if (audio->context_ready && !ma_device_is_started(ma_engine_get_device(&audio->engine))) return -1;
    return ma_sound_at_end(&audio->sound) ? 1 : 0;
}
OC2_API ma_uint64 oc2_audio_clock(oc2_audio* audio)
{ return ma_engine_get_time_in_pcm_frames(&audio->engine); }

/* Offline tests use the same graph; these entry points are absent from release binaries. */
#ifdef OC2_AUDIO_TEST
OC2_API int oc2_audio_open_offline(const float* samples, int frames, int rate, int channels, int loop, oc2_audio** result)
{ return open_audio(samples, frames, rate, channels, loop, 1, result); }
OC2_API int oc2_audio_render(oc2_audio* audio, float* output, unsigned int frames)
{ return ma_engine_read_pcm_frames(&audio->engine, output, frames, NULL); }
#endif
