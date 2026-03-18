# Contributing to Open Empires

Thank you for your interest in contributing to Open Empires!

## Reporting Bugs

- Use [GitHub Issues](../../issues) to report bugs
- Include steps to reproduce, expected behavior, and actual behavior
- Mention your Unity version and OS

## Making Changes

1. Fork the repository
2. Create a feature branch from `main` (`git checkout -b my-feature`)
3. Make your changes
4. Test that the game runs correctly in the Unity Editor
5. Commit your changes with a clear message
6. Push to your fork and open a Pull Request

## Development Environment

- **Unity**: 6000.3.9f1 (or compatible Unity 6 version)
- **Rust**: Latest stable (for backend work)
- **PostgreSQL**: Required for backend matchmaking

## Code Guidelines

- Simulation code (anything under `Scripts/Core/`, `Scripts/Squads/`, `Scripts/Territory/`, `Scripts/Combat/`) must use `Fixed32` math — no floats
- Keep sim and render code strictly separated
- New commands must be added to `CommandSerializer.cs` with a unique command type ID

## Questions?

Open an issue for questions or discussion about potential changes.
