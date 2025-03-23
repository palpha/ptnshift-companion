#include "pch.h" // or disable precompiled headers if you prefer
#include "WinScreenStreamLib.h"
#include <windows.h>
#include <dxgi1_2.h>
#include <d3d11.h>
#include <wrl/client.h>
#include <thread>
#include <atomic>
#include <vector>
#include <cstdio>
#include <chrono>

using Microsoft::WRL::ComPtr;

struct DisplayContext {
    int id;
    ComPtr<IDXGIOutput> output;
    int width;
    int height;
    char name[128];
    bool isPrimary;
};

// Global or static storage for enumerated displays
static std::vector<DisplayContext> gDisplays;

// We store duplication interface so we can properly release it in StopCapture()
static ComPtr<IDXGIOutputDuplication> gDuplication;
static ComPtr<ID3D11Device> gD3DDevice;
static ComPtr<ID3D11DeviceContext> gD3DContext;

// Thread control
static std::thread gCaptureThread;
static std::atomic<bool> gCaptureRun{ false };
static int gCurrentDisplayId = -1;
std::atomic<int> gFrameRate = 30;
static CaptureFrameCallback gCallback = nullptr;
static void* gCallbackUserContext = nullptr;

// Frame buffer for RGB pixels
static std::vector<unsigned char> gFrameBuffer;

// ------------------------------------------------------
// GetActiveDisplays
// ------------------------------------------------------
int GetActiveDisplays(DisplayInfo* infos, int maxCount)
{
    gDisplays.clear();

    ComPtr<IDXGIFactory1> factory;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&factory))) {
        printf("Failed to create DXGI factory\n");
        return 0;
    }

    UINT adapterIndex = 0;
    ComPtr<IDXGIAdapter1> adapter;
    int displayCount = 0;

    while (factory->EnumAdapters1(adapterIndex, &adapter) != DXGI_ERROR_NOT_FOUND) {
        UINT outputIndex = 0;
        ComPtr<IDXGIOutput> output;
        while (adapter->EnumOutputs(outputIndex, &output) != DXGI_ERROR_NOT_FOUND) {
            DXGI_OUTPUT_DESC desc;
            output->GetDesc(&desc);

            if (desc.AttachedToDesktop) {
                DisplayContext ctx;
                ctx.id = displayCount;
                ctx.output = output;
                ctx.width = desc.DesktopCoordinates.right - desc.DesktopCoordinates.left;
                ctx.height = desc.DesktopCoordinates.bottom - desc.DesktopCoordinates.top;

                WideCharToMultiByte(
                    CP_ACP, 0,
                    desc.DeviceName, -1,
                    ctx.name, 128,
                    NULL, NULL
                );
                ctx.name[127] = 0; // Ensure null termination

                // Identify primary display
                ctx.isPrimary = (desc.DesktopCoordinates.left == 0 && desc.DesktopCoordinates.top == 0);

                gDisplays.push_back(ctx);

                if (displayCount < maxCount && infos) {
                    infos[displayCount].id = displayCount;
                    strcpy_s(infos[displayCount].name, ctx.name);
                    infos[displayCount].width = ctx.width;
                    infos[displayCount].height = ctx.height;
                    infos[displayCount].isPrimary = ctx.isPrimary;
                }

                printf("Display %d: %s (%dx%d) %s\n",
                    displayCount, ctx.name, ctx.width, ctx.height,
                    ctx.isPrimary ? "[PRIMARY]" : "");

                displayCount++;
            }
            output.Reset();
            outputIndex++;
        }
        adapter.Reset();
        adapterIndex++;
    }

    return displayCount;
}

// ------------------------------------------------------
// Capture Thread (Desktop Duplication)
// ------------------------------------------------------
static void CaptureThread(int displayId)
{
    if (displayId < 0 || displayId >= (int)gDisplays.size()) {
        printf("Invalid displayId: %d\n", displayId);
        return;
    }

    DisplayContext& ctx = gDisplays[displayId];
    printf("Capture thread started for display #%d\n", displayId);

    // We already created gD3DDevice in StartCapture. Also gDuplication is set up there.
    // We'll just use them. No need to re-create a device here.

    // Acquire frames until gCaptureRun is false
    DXGI_OUTDUPL_FRAME_INFO frameInfo = {};
    ComPtr<IDXGIResource> desktopResource;
    ComPtr<ID3D11Texture2D> acquiredTex;

    auto lastFrameTime = std::chrono::steady_clock::now();

    while (gCaptureRun) {
        int frameDurationMs = 1000 / gFrameRate;
        printf("Capturing frame, expected interval: %d ms\n", frameDurationMs);

        desktopResource.Reset();
        acquiredTex.Reset();

        // AcquireNextFrame with 100ms timeout
        HRESULT hr = gDuplication->AcquireNextFrame(
            100,
            &frameInfo,
            &desktopResource
        );

        if (hr == DXGI_ERROR_WAIT_TIMEOUT) {
            // no new frame
            continue;
        }
        else if (FAILED(hr)) {
            // e.g. device lost or other error
            printf("AcquireNextFrame failed: 0x%08X\n", hr);
            break;
        }

        // Convert resource to texture
        hr = desktopResource.As(&acquiredTex);
        if (SUCCEEDED(hr)) {
            D3D11_TEXTURE2D_DESC desc;
            acquiredTex->GetDesc(&desc);

            // Create staging texture
            desc.Usage = D3D11_USAGE_STAGING;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            desc.BindFlags = 0;
            desc.MiscFlags = 0;

            ComPtr<ID3D11Texture2D> stagingTex;
            hr = gD3DDevice->CreateTexture2D(&desc, nullptr, &stagingTex);
            if (SUCCEEDED(hr)) {
                gD3DContext->CopyResource(stagingTex.Get(), acquiredTex.Get());

                D3D11_MAPPED_SUBRESOURCE map;
                hr = gD3DContext->Map(stagingTex.Get(), 0, D3D11_MAP_READ, 0, &map);
                if (SUCCEEDED(hr)) {
                    auto* pixels = reinterpret_cast<unsigned char*>(map.pData);
                    for (int y = 0; y < ctx.height; ++y) {
                        for (int x = 0; x < ctx.width; ++x) {
                            int srcIndex = y * map.RowPitch + x * 4;
                            int dstIndex = (y * ctx.width + x) * 3;
                            gFrameBuffer[dstIndex + 0] = pixels[srcIndex + 2]; // R
                            gFrameBuffer[dstIndex + 1] = pixels[srcIndex + 1]; // G
                            gFrameBuffer[dstIndex + 2] = pixels[srcIndex + 0]; // B
                        }
                    }
                    // int pitch = map.RowPitch; // you can pass pitch to your callback if needed

                    // Call user callback
                    if (gCallback) {
                        gCallback(gFrameBuffer.data(), ctx.width, ctx.height, gCallbackUserContext);
                    }
                    gD3DContext->Unmap(stagingTex.Get(), 0);
                }
            }

            // Frame rate limiting logic
            auto now = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - lastFrameTime).count();
            if (elapsed < frameDurationMs) {
                std::this_thread::sleep_for(std::chrono::milliseconds(frameDurationMs - elapsed));
            }
            lastFrameTime = std::chrono::steady_clock::now();
        }

        gDuplication->ReleaseFrame();
    }

    printf("Capture thread ending for display #%d\n", displayId);
}

// ------------------------------------------------------
// StartCapture
// ------------------------------------------------------
int StartCapture(int displayId, int frameRate, CaptureFrameCallback callback, void* userContext)
{
    if (displayId < 0 || displayId >= (int)gDisplays.size()) {
        printf("Invalid displayId in StartCapture: %d\n", displayId);
        return -1;
    }

    if (gCaptureRun) {
        // Already capturing
        return 0;
    }

    gCurrentDisplayId = displayId;
    gFrameRate = frameRate;
    gCallback = callback;
    gCallbackUserContext = userContext;

    // 1. Create D3D11 device (only once)
    HRESULT hr = D3D11CreateDevice(
        nullptr,                 // adapter
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,                 // software raster
        0,                       // flags
        nullptr, 0,             // feature levels
        D3D11_SDK_VERSION,
        &gD3DDevice,
        nullptr,                 // feature level
        &gD3DContext
    );
    if (FAILED(hr)) {
        printf("D3D11CreateDevice failed: 0x%08X\n", hr);
        return -2;
    }

    // 2. Duplicate output for the chosen display
    ComPtr<IDXGIOutput1> output1;
    hr = gDisplays[displayId].output.As(&output1);
    if (FAILED(hr)) {
        printf("As IDXGIOutput1 failed: 0x%08X\n", hr);
        return -3;
    }

    hr = output1->DuplicateOutput(gD3DDevice.Get(), &gDuplication);
    if (FAILED(hr)) {
        printf("DuplicateOutput failed: 0x%08X\n", hr);
        return -4;
    }

    // Set up frame buffer
    int bufferSize = gDisplays[displayId].width * gDisplays[displayId].height * 3;
    gFrameBuffer.resize(bufferSize);

    // Now we can run the thread
    gCaptureRun = true;
    gCaptureThread = std::thread(CaptureThread, displayId);

    return 0; // success
}

void SetFrameRate(int frameRate)
{
    if (frameRate > 0) {
        gFrameRate = frameRate;
        printf("Frame rate updated: %d FPS\n", gFrameRate.load());
    }
}

// ------------------------------------------------------
// StopCapture
// ------------------------------------------------------
void StopCapture()
{
    if (!gCaptureRun) {
        return; // not capturing
    }

    printf("Stopping capture...\n");

    // Signal thread to stop
    gCaptureRun = false;

    // Wait for capture thread
    if (gCaptureThread.joinable()) {
        gCaptureThread.join();
    }

    printf("Capture stopped.\n");

    // Cleanup global state
    gDuplication.Reset(); // release duplication interface
    gD3DContext.Reset();
    gD3DDevice.Reset();

    gCurrentDisplayId = -1;
    gCallback = nullptr;
    gCallbackUserContext = nullptr;

    gFrameBuffer.clear();
    gFrameBuffer.shrink_to_fit();
}

// ------------------------------------------------------
// Cleanup
// ------------------------------------------------------
void Cleanup()
{
    StopCapture();
    gDisplays.clear();
}