# Third-party packages

- **SkiaSharp 3.119.4** and **SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.4** — MIT package licenses. Google Skia native engine has separate BSD-style upstream notices; retain required notices in distributions. See https://github.com/mono/SkiaSharp.
- **Microsoft.CodeAnalysis.CSharp 4.14.0** — MIT. See https://github.com/dotnet/roslyn.

Package licenses remain with their respective copyright owners. Our MIT project LICENSE covers only our work. Verify transitive/native notices when packaging binaries.

Linux native assets are explicitly referenced for runtime portability. Windows assets are included through SkiaSharp dependencies. CI build and smoke-test status must be checked before claiming validation.
