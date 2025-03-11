#pragma once

#ifdef WINSCREENSTREAMLIB_EXPORTS
#define WINSCREENSTREAMLIB_API __declspec(dllexport)
#else
#define WINSCREENSTREAMLIB_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

    // Matches your existing struct
    typedef struct DisplayInfo {
        int id;
        char name[128];
        int width;
        int height;
        bool isPrimary;
    } DisplayInfo;

    // Same callback signature
    typedef void (*CaptureFrameCallback)(const unsigned char* pixels, int width,
        int height, void* userContext);

    WINSCREENSTREAMLIB_API int GetActiveDisplays(DisplayInfo* infos, int maxCount);
    WINSCREENSTREAMLIB_API int StartCapture(int displayId, int frameRate, CaptureFrameCallback callback, void* userContext);
    WINSCREENSTREAMLIB_API void SetFrameRate(int frameRate);
    WINSCREENSTREAMLIB_API void StopCapture();
    WINSCREENSTREAMLIB_API void Cleanup();

#ifdef __cplusplus
}
#endif