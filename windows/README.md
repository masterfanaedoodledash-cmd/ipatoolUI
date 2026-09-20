## Windows build

The repository now includes a Windows companion app under `windows/`. It is a native WinForms launcher that lets you select `ipatool.exe`, pass command-line arguments, and view stdout/stderr output.

The **Build Windows App** workflow publishes a self-contained `win-x64` executable and a ZIP artifact on pushes and pull requests that change the Windows project. The original SwiftUI application remains the native macOS client.
