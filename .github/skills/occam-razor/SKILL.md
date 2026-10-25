---
name: occam-razor
description: "Apply Occam's razor (parsimony) to every decision, change, and explanation. Prefer the option with the fewest assumptions, parts, files, dependencies, and rules that still fully meets the requirement. Use for code changes, documentation, debugging, design choices, and explanations. Never use it as a reason to drop required behaviour, safety, error handling, or checks."
---

# Occam's razor

## Principle

Do not multiply entities beyond necessity. When several options explain or solve a problem equally well, choose the one with the fewest assumptions, parts, files, dependencies, and rules.

The principle is usually attributed to William of Ockham (14th century). A common phrasing is: plurality should not be posited without necessity, and it is futile to do with more what can be done with fewer.

## Limits

These limits are mandatory.

- The razor is a heuristic. It does not prove anything. A simpler option is not automatically correct.
- Use the razor only when the options are equally adequate. If the evidence needs a more complex explanation, choose the complex one.
- Never remove what the requirement, the user, the project rules, safety, error handling, or required checks need. Removing those is not simplicity. It is a defect.
- Simplicity never settles a question you cannot answer. In that case, ask the user, as AGENTS.md requires.

## Before adding anything

Before you create a file, function, class, parameter, option, dependency, abstraction, step, rule, or comment, name the specific requirement it serves. If you cannot name one, do not add it.

## Code

- Make the smallest change that meets the requirement.
- Do not add speculative features, extension points, configuration, generic layers, or base classes for a single use.
- Before writing new code, check whether the repository already has a helper that does the job. Reuse it.
- Do not add a dependency when existing ones can do the job.
- Do not refactor, rename, or reformat code that the task does not require.
- Do not split a simple function into several without a requirement that needs the split.
- Do not write comments that restate the code. Keep only a comment that records a constraint the code cannot express.

## Documentation and rules

- Write only rules that are required. Delete duplicates.
- A new rule must prevent a concrete, named mistake. If it prevents none, do not write it.
- Describe the current state. Keep change history in version control, not in the document.

## Debugging and explanations

- Start with the simplest explanation that fits all the observations. Add a more complex explanation only when the evidence rules out the simple one.
- Check the simplest causes first: a typo, wrong input, a stale build, the wrong file. Only then consider architecture.
- State each assumption you need, and keep the number of assumptions small.

## Choosing between options

- If two options both meet the requirement, pick the one with fewer moving parts, fewer new files, and fewer assumptions.
- Say which option you chose and why, in one line.
- If the simpler option fails a requirement, it is not actually simpler. Choose the option that meets the requirement.

## Checks before finishing

1. Does every new piece serve a named requirement?
2. Could the same result be reached with less code, fewer files, or fewer rules?
3. Did removing anything break a requirement, a check, or an error path?
4. Did the change touch only what the task needs?

## Examples

- Bad: a factory, an interface, and a config option to support one case. Good: one function that handles that case.
- Bad: a new document that repeats a rule already in AGENTS.md. Good: no new document.
- Bad: retry logic, caching, and a queue for a one-time script. Good: the direct call, plus the error check the project requires.
