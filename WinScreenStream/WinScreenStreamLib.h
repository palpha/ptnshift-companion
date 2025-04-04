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
        float dpiX;
        float dpiY;
        int left;
        int top;
    } DisplayInfo;

    // Same callback signature
    typedef void (*CaptureFrameCallback)(const unsigned char* pixels, int width,
        int height, void* userContext);

    WINSCREENSTREAMLIB_API int __stdcall GetActiveDisplays(DisplayInfo* infos, int maxCount);
    WINSCREENSTREAMLIB_API int __stdcall StartCapture(int displayId, int frameRate, CaptureFrameCallback callback, void* userContext);
    WINSCREENSTREAMLIB_API void __stdcall SetFrameRate(int frameRate);
    WINSCREENSTREAMLIB_API void __stdcall StopCapture();
    WINSCREENSTREAMLIB_API void __stdcall Cleanup();
    // WINSCREENSTREAMLIB_API void __stdcall ScaleSubregionGPU(
    //     const unsigned char* fullFrame,
    //     int srcWidth,
    //     int srcHeight,
    //     int cropX,
    //     int cropY,
    //     int cropWidth,
    //     int cropHeight,
    //     int outWidth,
    //     int outHeight,
    //     unsigned char* outScaled
    // );

#ifdef __cplusplus
}
#endif
