# Agent Rules

## Purpose

This file defines the minimum working procedure for agents and contributors operating in this repository.

## Required Workflow Before Solving A Task

1. Read the task carefully and identify its real target:
   UI layout, build issue, feature behavior, persistence, runtime dock logic, or architecture question.
2. Check `src/docs/PROJECT_MAP.md` to see where the relevant code most likely lives.
3. If the task involves an error, build failure, or recurring technical issue, check `src/docs/TROUBLESHOOTING.md` before starting deeper debugging.
4. Only after that start reading and changing files.

## Required Workflow When Errors Appear

1. First compare the current error with entries in `src/docs/TROUBLESHOOTING.md`.
2. If the same or a very similar issue already exists there, reuse that path of investigation first.
3. If the issue is new, solve it normally.
4. After the new issue is solved, add a new entry to `src/docs/TROUBLESHOOTING.md` so the next pass starts from known ground.

## Documentation Rules

Before large or repeated work, use these files:

- `src/docs/PROJECT_MAP.md`
  For orientation in the repository.
- `src/docs/TROUBLESHOOTING.md`
  For known failures and fixes.
- `src/docs/ARCHITECTURE.md`
  For high-level system shape.

## Change Rules

- Do not start blind edits without identifying the owning area first.
- Prefer fixing root layout or structure problems over cosmetic one-off patches.
- If a bug repeats in multiple places, document the pattern, not just the instance.
- When a debugging session produced a reliable command or environment setup, record it in `src/docs/TROUBLESHOOTING.md`.
- When adding a new feature or materially extending existing behavior, document it as well.
- Feature documentation should be added to the most relevant doc in `src/docs/`, and if no suitable file exists, create one.

## Build Rules

- Be careful with restore and build behavior in offline or sandboxed environments.
- If NuGet-related failures appear, check `src/docs/TROUBLESHOOTING.md` before assuming code is broken.
- If DockLauncher is already running and a task requires rebuilding it, close the running DockLauncher process before starting the rebuild so locked binaries and stale runtime state do not affect the result.

## Expected Outcome

Every task should leave the repository in a better documented state:

- code changed if needed
- repeated failures documented
- future entry points easier to find
