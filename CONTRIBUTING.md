# C# Style and Project Organization

These conventions apply to all new code, modifications, tests, and scheduled code reviews.

## File organization

- One primary class per `.cs` file. Do not define unrelated helper classes in the same file.
- One `struct`, `record struct`, `enum`, or `interface` per file.
- Use a filename matching the declared type, e.g. `Watermark.cs`, `Manifest.cs`.
- Put types in dedicated folders by category: `Classes/`, `Structs/`, `Enums/`, `Interfaces/`, `Records/`. Use `Functions/` for standalone static function/helper modules if needed, but prefer a clearly named service or utility class with one class per file.
- Use separate files for program entrypoint, CLI parsing, image watermarking, verification, code fingerprinting and evidence manifests.
- Do not introduce nested support types just to avoid placing them in separate files.

## C# type spelling

- Always use CLR/.NET type names rather than C# language aliases in source code.
- Examples: `String` not `string`, `Int32` not `int`, `Int64` not `long`, `Boolean` not `bool`, `Double` not `double`, `Byte` not `byte`, `Object` not `object`, `Char` not `char`, `Decimal` not `decimal`, and `Void` is not used for return types (C# requires `void`).
- Preserve valid C# syntax: `void` for methods with no return value, and `var` only when appropriate and consistent with explicit CLR typing preferences.
- Nullable reference types should use `String?`, `Object?`, etc.
- Follow the same convention for method signatures, locals, generics, and declarations.

## Existing code

Existing files predate these rules; refactor to this layout incrementally, keeping behavior and tests unchanged. Verify builds before claiming successful refactoring.

## Review quality

- Test changes, do not describe uncompiled source as validated.
- Keep secrets, candidate materials and private evidence out of Git.
