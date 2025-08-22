# P/Invoke & Memory Management:
- .NET side must immediately copy all data and free Swift-allocated pointers using Marshal.FreeHGlobal()
- On macOS, .NET's `Marshal.FreeHGlobal` is compatible with pointers from `strdup` and can be used to free them.
- For maximum portability, you may use native `free` via `[DllImport("libc")]`, but `Marshal.FreeHGlobal` is acceptable for current macOS usage.
- Always copy data immediately and free pointers after use.
- Track all allocated pointers for proper cleanup.
- Return managed types (records/classes) from public APIs, not raw P/Invoke structs

# Naming & Style Conventions:
- Use camelCase for private fields (no underscore prefix)
- Use auto properties when possible and when not apparently detrimental to performance
- Use record types with primary constructors for simple data structures
- Maintain a good degree of separation of concerns, suggest refactoring where appropriate
- Follow the existing .editorconfig rules - check for lower_camel_case_style for private static fields
- Match the established patterns in the codebase rather than assuming standard conventions
- Prefer explicit == false instead of prefix ! for boolean checks

# Performance & Safety:
- Always include proper error handling and cleanup in P/Invoke callback scenarios
- Prefer copying data immediately rather than holding onto unsafe pointers
