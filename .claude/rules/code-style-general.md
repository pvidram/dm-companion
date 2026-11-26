# Code standards: general

Applies to every project, regardless of stack.

## Names and files

- Code files: `kebab-case` (e.g. `user-profile.service.ts`, `invoice-controller.cs`).
- Classes, types, interfaces, components: `PascalCase`.
- Variables, functions, props: `camelCase`.
- Configuration constants: `SCREAMING_SNAKE_CASE` only if they are environment constants.
- Descriptive names. `list` is not a name, `selectedInvoices` is.

## Size of things

- Code files: prefer fewer than 300 lines. If longer, split by responsibility.
- Functions: prefer fewer than 40 lines. If longer, they probably do several things.
- Parameters: 3-4 at most. If you need more, use an object/DTO.

## Comments

- Only when the "why" is not obvious (invariant, documented workaround, known bug).
- Never to explain what the code already says.
- Never references to tickets or authors; that belongs in the commit and in git blame.

## Errors and logs

- Do not swallow exceptions (empty `catch { }`) without a stated reason.
- Do not use `console.log` / `Console.WriteLine` / `print` in production code.
- Error messages must be useful to whoever reads them.

## Git

- Conventional commits: `feat:`, `fix:`, `refactor:`, `docs:`, `chore:`, written in English.
- Atomic commits. One commit = one logical change. Do not mix a refactor with a bug fix.
- Do not commit: `.env*`, `node_modules/`, `bin/`, `obj/`, `.vs/`, credentials.
