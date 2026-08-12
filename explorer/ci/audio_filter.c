// audio_filter.c — DYLD interpose shim that filters broken paravirt audio
// devices out of CoreAudio device enumeration.
//
// Purpose: on GitHub-hosted `macos-14` runners the paravirt audio HAL
// (`Apple Virtual Sound Device`, served by
// /System/Library/Audio/Plug-Ins/HAL/AppleVirtIOSound.driver) responds to
// `kAudioHardwarePropertyDevices` enumeration but its AudioUnit blocks
// indefinitely on `AudioUnitSetProperty` → `mach_msg` to coreaudiod.
// When Decentraland's `rust_audio` (com.decentraland.livekit-sdk) calls
// `cpal::supports_input` for every device in the list during
// `VoiceChatPlugin.InitializeAsync`, it hits this device and Unity's
// main thread wedges forever. Removing the broken device from the
// HAL on-disk requires defeating SSV (sealed system volume — requires
// Recovery Mode + bputil + reboot, not possible on ephemeral runners),
// and `kAudioDevicePropertyIsHidden` is not client-settable. The only
// remaining knob is intercepting the enumeration call itself inside the
// Explorer process via `DYLD_INSERT_LIBRARIES`.
//
// Strategy: interpose `AudioObjectGetPropertyData`. When the call asks
// the system object for the device list, run the original call, then
// scan the returned `AudioDeviceID[]` and drop any device whose name
// matches a known-bad pattern (`Apple Virtual Sound Device`,
// `Null Audio Device`). Shrink `*ioDataSize` accordingly. All other
// calls (specific-device queries, non-Devices selectors, errors) pass
// through unchanged.
//
// Recursion: dyld interpose only redirects calls from *other* modules;
// references to `AudioObjectGetPropertyData` from inside this dylib
// resolve to the original symbol, so the name-query inside
// `device_should_be_dropped` is not recursive.
//
// Build:
//   clang -dynamiclib -O2 \
//     -framework CoreAudio -framework CoreFoundation \
//     explorer/ci/audio_filter.c -o /tmp/audio_filter.dylib
//
// Use:
//   DYLD_INSERT_LIBRARIES=/tmp/audio_filter.dylib /path/to/Explorer.app/Contents/MacOS/Explorer
//
// Caveat: macOS strips DYLD_* from any process invoked via `/usr/bin/open`
// (LaunchServices honours SIP). The Explorer must be exec'd directly
// (e.g. via MetaForge's `directExec: true` path) for this shim to load.

#include <CoreAudio/CoreAudio.h>
#include <CoreFoundation/CoreFoundation.h>

#include <stdio.h>
#include <string.h>

// ── Once-only "loaded" notice so we can grep Player.log for "[audio_filter]"
// and confirm the shim actually attached to the Explorer process.
__attribute__((constructor))
static void audio_filter_init(void)
{
    fprintf(stderr,
        "[audio_filter] loaded — will drop Apple Virtual Sound Device and "
        "Null Audio Device from kAudioHardwarePropertyDevices enumeration\n");
}

// Returns 1 if the device matches a known-bad name and should be dropped
// from enumeration, 0 otherwise. Querying the device's name property is
// a cheap CoreAudio call that does not open an AudioUnit, so it does not
// share the `AudioUnitSetProperty`-on-paravirt hang path.
static int device_should_be_dropped(AudioDeviceID dev)
{
    AudioObjectPropertyAddress name_addr = {
        kAudioObjectPropertyName,
        kAudioObjectPropertyScopeGlobal,
        kAudioObjectPropertyElementMain
    };
    CFStringRef name_ref = NULL;
    UInt32 size = (UInt32)sizeof(name_ref);
    OSStatus st = AudioObjectGetPropertyData(
        dev, &name_addr, 0, NULL, &size, &name_ref);
    if (st != noErr || name_ref == NULL) {
        return 0;
    }

    char name_buf[256];
    name_buf[0] = '\0';
    CFStringGetCString(name_ref, name_buf, sizeof(name_buf), kCFStringEncodingUTF8);
    CFRelease(name_ref);

    int drop =
        strstr(name_buf, "Apple Virtual Sound Device") != NULL ||
        strstr(name_buf, "Null Audio Device") != NULL;

    if (drop) {
        // Logged once per (dev, name) pair the system reports; on a steady
        // configuration this is at most 3 lines total. Surfaces in Player.log.
        fprintf(stderr, "[audio_filter] dropping device id=%u name=\"%s\"\n",
                (unsigned)dev, name_buf);
    }
    return drop;
}

// Interposed replacement. Forwards to the original, then optionally
// filters the result buffer.
static OSStatus audio_filter_AudioObjectGetPropertyData(
    AudioObjectID                       inObjectID,
    const AudioObjectPropertyAddress*   inAddress,
    UInt32                              inQualifierDataSize,
    const void*                         inQualifierData,
    UInt32*                             ioDataSize,
    void*                               outData)
{
    OSStatus st = AudioObjectGetPropertyData(
        inObjectID, inAddress, inQualifierDataSize, inQualifierData,
        ioDataSize, outData);

    // Only filter the full-system device-list query. All other property
    // reads (names, formats, per-device state, etc.) pass through.
    if (st != noErr ||
        inObjectID != kAudioObjectSystemObject ||
        inAddress == NULL ||
        inAddress->mSelector != kAudioHardwarePropertyDevices ||
        outData == NULL || ioDataSize == NULL)
    {
        return st;
    }

    AudioDeviceID* devs = (AudioDeviceID*)outData;
    UInt32 count = *ioDataSize / (UInt32)sizeof(AudioDeviceID);
    UInt32 kept = 0;
    for (UInt32 i = 0; i < count; ++i) {
        if (!device_should_be_dropped(devs[i])) {
            devs[kept++] = devs[i];
        }
    }
    *ioDataSize = kept * (UInt32)sizeof(AudioDeviceID);
    return noErr;
}

// macOS DYLD_INTERPOSE — Mach-O __DATA,__interpose section magic. dyld
// reads the {replacement, replacee} pairs at load time and rewrites
// import-table entries for every other module to point at our function.
// References from inside *this* dylib (e.g. the call in
// device_should_be_dropped) still bind to the real symbol.
#define DYLD_INTERPOSE(_replacement, _replacee)                                 \
    __attribute__((used)) static struct {                                       \
        const void* replacement;                                                \
        const void* replacee;                                                   \
    } _interpose_##_replacee __attribute__((section ("__DATA,__interpose"))) =  \
        { (const void*)(unsigned long)&_replacement,                            \
          (const void*)(unsigned long)&_replacee };

DYLD_INTERPOSE(audio_filter_AudioObjectGetPropertyData, AudioObjectGetPropertyData)
