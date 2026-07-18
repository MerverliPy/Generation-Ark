# Validation notes

The code was generated in an environment without a .NET SDK, so the C# projects could not be compiled or executed here.

Validation performed in this environment:

- Project and solution references checked.
- Every C# source file is present under the expected namespace and folder.
- No Unity APIs or wall-clock values appear in authoritative simulation code.
- Deterministic ordering keys are explicit.
- State checksum enumeration uses ordinally sorted state.
- Frame backlog is retained after per-frame tick limits.
- Tests cover the first milestone acceptance contract.

Run the commands in `README.md` on a machine with .NET 8 to perform the authoritative compile and test pass.
