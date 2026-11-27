---
name: occam-razor
description: Apply Occam's razor to option choice, code changes, documentation, debugging, and explanations. Use when two options satisfy the same requirement, or when the user asks for the simplest or minimal change.
version: 2.0.0
---

# Occam's Razor

## <a id="oc-1"></a>OC-1 Requirements override simplicity

- <a id="oc-1-1"></a>OC-1.1 You MUST NOT choose an option that fails a requirement. You MUST NOT remove anything that a requirement, user rule, project rule, safety measure, error-handling path, or required check demands. Such removal is a defect, not a simplification.

## <a id="oc-2"></a>OC-2 Option selection

- <a id="oc-2-1"></a>OC-2.1 For choices permitted by [AG-8](../../AGENTS.md#ag-8), when options satisfy the same requirements, prefer fewer assumptions, components, dependencies, and rules, unless that increases maintenance or execution cost.
- <a id="oc-2-2"></a>OC-2.2 State the chosen option and reason in one line when the choice affects the user.
- <a id="oc-2-3"></a>OC-2.3 This principle is a heuristic: simpler is not automatically correct.

## <a id="oc-3"></a>OC-3 Debugging and explanation

- <a id="oc-3-1"></a>OC-3.1 Start from the simplest assumption that explains all observations. Add complexity only when the observations require it.
- <a id="oc-3-2"></a>OC-3.2 Check common causes first: typo, wrong input, stale build, wrong file.
- <a id="oc-3-3"></a>OC-3.3 List the assumptions needed ([AG-T6](../../AGENTS.md#ag-t6)).

## <a id="oc-4"></a>OC-4 Additions

- <a id="oc-4-1"></a>OC-4.1 Add a file, function, class, parameter, option, dependency, abstraction, step, rule, or comment only if it serves an identifiable requirement. A new rule must prevent an identifiable defect.

## <a id="oc-5"></a>OC-5 Code

- <a id="oc-5-1"></a>OC-5.1 Make the smallest change that satisfies the requirement. Do not refactor, rename, or reformat code the task does not require.
- <a id="oc-5-2"></a>OC-5.2 Before adding code or a dependency, reuse an existing implementation or dependency that satisfies the requirement.

## <a id="oc-6"></a>OC-6 Self-check

Before finishing, verify ([AG-T8](../../AGENTS.md#ag-t8)) that [OC-1.1](#oc-1-1), [OC-4.1](#oc-4-1), and [OC-5.1](#oc-5-1) hold.
