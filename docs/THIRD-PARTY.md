# Third-party packages

- **SkiaSharp 3.119.4** and **SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.4** — MIT package licenses. Google Skia native engine has separate BSD-style upstream notices; retain required notices in distributions. See https://github.com/mono/SkiaSharp.
- **Microsoft.CodeAnalysis.CSharp 4.14.0** — MIT. See https://github.com/dotnet/roslyn.

- **xunit.v3 3.2.2** and **xunit.runner.visualstudio 3.1.5** — Apache-2.0, test-only. See https://github.com/xunit/xunit.
- **Microsoft.NET.Test.Sdk 17.14.1** — MIT, test-only. See https://github.com/microsoft/vstest.

Package licenses remain with their respective copyright owners. Our MIT project LICENSE covers only our work. Verify transitive/native notices when packaging binaries.

Linux native assets are explicitly referenced for runtime portability. Windows assets are included through SkiaSharp dependencies. No hosted CI is configured. Build and smoke-test locally before claiming validation; review transitive package and native-library notices before redistributing binaries.
