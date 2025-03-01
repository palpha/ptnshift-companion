# Liveshift Companion

Companion app for Tim Exile's amazing Liveshift, the Push 2-compatible variant of [Scapeshift](https://www.patreon.com/c/timexile/shop).

## Licensing

This project is licensed under the GNU General Public License v3.0 (GPLv3), ensuring that any modifications or derivative works are also made available under the same license.

However, if you are interested in using this project under different terms—for example, to distribute a modified version without being bound by GPLv3—you may contact the project maintainer to discuss alternative licensing options. Additional permissions may be granted on a case-by-case basis.

For inquiries, please reach out via https://github.com/palpha/liveshift-companion/issues.

## libusb relinking

This software dynamically links to libusb (LGPL). If you wish to replace libusb.dylib with a modified version in a macOS build of this software, you may do so by swapping the .dylib file in Contents/MonoBundle/ without needing to rebuild the application.