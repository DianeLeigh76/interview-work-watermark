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

## Braces and formatting

- Use Allman brace style everywhere: every opening `{` and closing `}` must be on its own line, aligned with its associated declaration or statement.
- This applies to namespaces, classes, structs, interfaces, enums, records, methods, constructors, properties, control-flow blocks, and initializer blocks where syntactically applicable.
- Always use braces for `if`, `else`, `for`, `foreach`, `while`, and similar control-flow bodies, including single-statement bodies.
- Do not use inline single-line blocks such as `if (condition) { DoWork(); }`.
- Enforce these rules in code reviews and apply them while refactoring existing source.

## Method size and nesting

- Aim for no more than three levels of control-flow nesting inside a method; at four levels, consider extracting a named helper or using guard clauses.
- Namespace, type and method declaration indentation does not count toward this control-flow guideline.
- Keep command dispatch separate from command implementation, validation and result reporting.
- Prefer descriptive methods that encapsulate one responsibility over deeply nested `switch`, `foreach`, `if` and `try` blocks.
- Do not mechanically extract tiny blocks if doing so obscures behavior or increases complexity.
- The interactive menu is the default for no-argument launches from both Visual Studio and a normal terminal; explicit CLI arguments must continue to work for automation.

## Owner preference: shallow, readable control flow

These are review requirements for all contributors, including AI-generated changes:

- Avoid deep indentation as a design rule, not just a formatting preference. Count nested control-flow constructs within a method; namespace, type, and method indentation are excluded.
- Target zero to three nested control-flow levels. At four or more, refactor using guard clauses, early returns, or focused helper methods unless there is a clear, documented reason not to.
- Every top-level command `switch` case should delegate to one appropriately named handler method and return its result. Do not implement substantial business logic directly inside a case.
- Handler methods should delegate validation, processing, and reporting when these responsibilities would otherwise cause large or deeply nested methods.
- Keep code readable and debuggable. Do not introduce unnecessary abstractions merely to satisfy a numeric indentation limit.
- The same interactive, plain-language menu must appear for no-argument launches in Visual Studio and when running the executable directly from a terminal. CLI argument mode must remain supported for scripts.
- When modifying command dispatch or menu code, review both launch paths and report whether they were actually tested.

The owner locally confirmed that the solution built, tests passed, and both launch modes appeared to work after the command-dispatch refactor. This is a historical validation report, not proof that later revisions pass.
