---
name: occam-razor
description: "Apply Occam's razor to decisions, changes, and explanations. Prefer the option with fewest assumptions, parts, files, dependencies, and rules that still meets the requirement. Use for code changes, documentation, debugging, design choices, and explanations. Never use it to drop required behaviour, safety, error handling, or checks."
---

# Occam's razor

## Principle

Do not multiply entities beyond necessity. When options solve a problem equally well, choose the one with fewest assumptions, parts, files, dependencies, and rules.

## Limits

- Heuristic; simpler is not automatically correct.
- Use only when options are equally adequate. If evidence needs a more complex explanation, choose it.
- Never remove what requirement, user, project rules, safety, error handling, or required checks need; that is a defect, not simplicity.
- Simplicity does not settle unanswerable questions; ask the user, as AGENTS.md requires.

## Before adding anything

Name the specific requirement a new file, function, class, parameter, option, dependency, abstraction, step, rule, or comment serves. If none, do not add it.

## Code

- Make the smallest change that meets the requirement.
- Do not add speculative features, extension points, configuration, generic layers, or base classes for one use.
- Check for an existing helper before writing new code; reuse it.
- Do not add a dependency if existing ones can do the job.
- Do not refactor, rename, or reformat code the task does not require.
- Do not split a simple function without a requirement.
- Do not write comments that restate code. Keep only comments recording a constraint code cannot express.

## Documentation and rules

- Write only required rules; delete duplicates.
- A new rule must prevent a concrete, named mistake; otherwise do not write it.
- Describe the current state. Keep change history in version control, not the document.

## Debugging and explanations

- Start with the simplest explanation that fits all observations. Add complexity only when evidence rules out the simple one.
- Check simplest causes first: typo, wrong input, stale build, wrong file. Only then consider architecture.
- State each assumption needed; keep the number small.

## Choosing between options

- If two options meet the requirement, pick the one with fewer moving parts, new files, and assumptions.
- Say which option you chose and why, in one line.
- If the simpler option fails a requirement, it is not simpler. Choose the option that meets the requirement.

## Checks before finishing

1. Does every new piece serve a named requirement?
2. Could the same result use less code, fewer files, or fewer rules?
3. Did removing anything break a requirement, check, or error path?
4. Did the change touch only what the task needs?

## Examples

- Bad: a factory, interface, and config option for one case. Good: one function for that case.
- Bad: a new document repeating a rule already in AGENTS.md. Good: no new document.
- Bad: retry logic, caching, and a queue for a one-time script. Good: the direct call plus required error check.