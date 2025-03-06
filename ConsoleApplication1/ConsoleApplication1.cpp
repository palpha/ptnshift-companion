#include <iostream>
#include <thread>
#include <chrono>

// 1. Include your library header
#include "..\\WinScreenStream\\WinScreenStreamLib.h"

#pragma comment(lib, "Y:\\lsc-windows\\ARM64\\Debug\\WinScreenStream.lib") 
// Ensures we link against WinScreenStreamLib (import library)

// Callback for frame capture
static void __cdecl MyFrameCallback(
    const unsigned char* pixels,
    int width,
    int height,
    void* userContext
)
{
    // We'll just print a message. Real code might copy or process 'pixels'.
    std::cout << "[Callback] Captured frame: " << width << " x " << height << std::endl;
}

int main()
{
    std::cout << "=== WinScreenStream Test ===" << std::endl;

    // 2. Get active displays
    DisplayInfo displays[10];
    int count = GetActiveDisplays(displays, 10);
    std::cout << "Found " << count << " displays." << std::endl;

    if (count == 0) {
        std::cout << "No displays found, exiting." << std::endl;
        return 0;
    }

    // 3. Pick the first display (for example)
    int displayId = 0;
    std::cout << "Using display #" << displayId
        << " name: " << displays[displayId].name
        << " isPrimary: " << (displays[displayId].isPrimary ? "true" : "false")
        << std::endl;

    // 4. Start capture
    int hr = StartCapture(displayId, MyFrameCallback, nullptr);
    if (hr != 0) {
        std::cout << "StartCapture failed with code: " << hr << std::endl;
        return 1;
    }

    std::cout << "Capture started successfully. We'll capture for ~5 seconds..." << std::endl;
    // Let the capture thread run for 5 seconds
    std::this_thread::sleep_for(std::chrono::seconds(5));

    // 5. Stop capture
    std::cout << "Stopping capture..." << std::endl;
    StopCapture();
    std::cout << "Capture stopped." << std::endl;

    // 6. Clean up
    std::cout << "Calling Cleanup()..." << std::endl;
    Cleanup();
    std::cout << "Cleanup returned. Exiting." << std::endl;

    return 0;
}