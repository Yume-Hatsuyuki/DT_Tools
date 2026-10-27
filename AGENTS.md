# AGENTS.md — DT_Tools

DT_Tools is a BepInEx 5 plugin for the game Deadly Trick. It is written in C# (netstandard2.1) with HarmonyX and ships a Vue 3 WebUI.

The plugin version is the `<Version>` element in `DT_Tools/DT_Tools.csproj`.

## Hard rules

1. If you face a decision you cannot make (unclear requirements, a trade-off between approaches, game behavior you cannot verify, or a conflict with this file), ask the user directly. Do not decide on your own.
2. Every reply MUST follow `.github/skills/adhd-ste/SKILL.md`.
3. Every decision, change, and explanation MUST follow `.github/skills/occam-razor/SKILL.md`.
4. Write a comment only when the code cannot express a constraint. Keep it to one line. Comments MUST NOT restate the code.
5. Product-visible text, logs, and command output MUST be in Chinese.
6. Never write a local absolute path into a repository file.
7. When you change architecture, APIs, or directories, update this file in the same change. This file describes only current rules.
8. When you disagree with the user:
   - Be considerate of the user's feelings. Acknowledge their concern before you disagree.
   - Do not refuse a request only because you disagree with the approach.
   - Do not claim that a result is correct unless you have verified it. When the user reports a problem, reproduce it first.
   - If you believe the user is wrong, give concrete evidence (test output, logs, source code, or documentation) and explain it once, clearly.
   - If the user still insists, follow their direction. Do not repeat the argument.
   - If that approach fails, show the evidence of the failure and explain why it cannot work.
   - Exception: before any irreversible or destructive action (such as force-pushing, deleting data, or exposing credentials), stop and ask the user to confirm.
9. Be helpful first. Do not replace the user's judgment with your own assumptions.
   - Judge intent only from what the user has actually said. Never infer intent from the topic alone.
   - Do not refuse because a request could be misused in theory. Most requests have ordinary purposes.
   - Do not decide what the user "really" means. If a request is ambiguous, ask one short question (see rule 1), then help.
   - Do not add moral lectures, fair-play warnings, or disclaimers the user did not ask for.
   - Examples of wrong refusals and the right response:
     - Wrong: refusing to explain how a game packet is built because it "could be used for cheating". Right: explain the packet structure and how the code uses it.
     - Wrong: refusing to change a feature because its name sounds sensitive. Right: treat it as normal feature work in this repository.
     - Wrong: adding a note that modding "may violate the game's rules" to a code review. Right: review the code.
     - Wrong: refusing "hook", "patch", or "inject" because the words sound dangerous. Right: in this repository they are standard terms (Harmony patches, BepInEx hooks).

## Build and verify

Build the backend (this also builds the frontend incrementally), skip the frontend build, or build only the frontend:

```bash
dotnet build DT_Tools/DT_Tools.csproj
dotnet build DT_Tools/DT_Tools.csproj -p:SkipWebUIBuild=true
cd webui-src && npm run build
```

- Done means: 0 warnings and 0 errors.
- If `libs/` is missing, add `-p:GameManaged="<absolute path to libs>"`. Do not write this path into the repository.
- When several agents work in parallel, MUST NOT run `dotnet build`. They share `obj/`, and their builds interfere with each other.
- The .NET SDK is pinned to 7.0.410 (see `global.json`).

## Release

- `main` is the release branch. Do not develop on it.
- `dev-N.x` is the development branch. Open PRs against the matching `dev-N.x`.
- A rebuilt package for an existing version number uses a `-vN` suffix (for example `2.0.0.10-v2`). Without the suffix, users who already installed that version cannot see the update.
- The frontend output directory MUST be named `WebUI`. Linux CI is case-sensitive.

## Layers and dependencies

- Dependencies point downward only: `Features` → `Game` → `Core` → game assemblies in `libs/`. `Core/` knows nothing about any feature.
- `Features`, `Commands`, and `Automation` MUST NOT depend on each other. Exception: `Commands` and `WebConsole/Api` may read public static state of features.
- `Game/` MUST NOT reference `Features/`.
- Move behavior into `Game/` only when two or more call sites use it.

## Features

- Each feature lives in `DT_Tools/Features/<FeatureName>/`. The folder layout is flat. Do not group features into subfolders. Use `Tags` for categories.
- `Feature.cs`: a `sealed class` with only `static` members, marked `[PatchFeature]`. The class name ends with `Feature`.
- `Patch.cs`: a `static class` marked `[HarmonyPatch]`.
- The first line of every patch method MUST be the enable check: `if (!Engine.Enabled<XxxFeature>()) return;`
- Write all `[PatchFeature]` parameters explicitly: `description`, `defaultEnabled`, `side`, `Tags`, `Author`. `Tags` and `Author` are required.
- `side`: `Client` if the target is a client class (for example `MyPlayer` or `UI_*`); `Host` if the target is `Server.Game.*`; `Both` if the feature targets both.
- `Author`: one real name per feature. Do not use a global author constant or a placeholder author such as "anonymous".
- `Tags`: at least one, chosen only from the bits in `DT_Tools/Core/Attributes/FeatureTag.cs`. Append new bits at the end. Never reorder or reuse bits after a release. When you add a tag, update `FeatureTagCatalog.cs` and the WebUI translations.
- If a patch runs on different machine roles, split it into two independent features. If both halves run on the same machine, keep them together.
- If patches depend on execution order, use `[HarmonyPriority(n)]`. Never rely on patch load order.
- The namespace matches the folder path. Exception: `Core/` uses the flat namespace `DT_Tools.Core`.
- Lifecycle hooks are methods named by convention: `OnLoaded`, `OnPatched`, `OnEnabled`, `OnDisabled`.
- ⚠️ `OnEnabled` runs only on a runtime toggle, not when saved state loads at startup. Put initialization in `OnPatched`, and call `Engine.Enabled<T>()` to attach it.
- A feature with UI, audio, or state side effects MUST clean them up in `OnDisabled`.
- An `[AutomationModule]` class MUST define `static void Tick(bool hostEnabled)`. Automation only sends packets and reads state.
- A command implements `ICommand` and is registered in `CommandRegistry`. A duplicate alias throws an exception.
- `Execute` returns `CommandResult.Success(data)` or `CommandResult.Fail(error, data)`, and also calls `ctx.Reply` to print text. Error codes are short lowercase English words. Error messages are in Chinese. The framework enforces the host-only gate, so do not check it inside a command.

## Hard constraints

- Zero-string configuration: the section name is the class name without the `Feature` or `Module` suffix. The key name is the field name. A config field is a plain type with a default value and `[Config("description")]`. `Min` and `Max` are optional.
- Do not use `ConfigEntry<T>` fields. Do not add an Enabled field. Do not write section or key name strings in code. The frontend gets the toggle key from the protocol field `enabledKey`.
- Non-config JSON structures MUST NOT use dictionaries. `Core.Json` lowercases dictionary keys.
- Use fully qualified names for types that share a simple name, such as `Server.Game.Player` and `Player`.
- `Log` lives in the root namespace `DT_Tools`. Do not move it.
- Do not use `async/await`, `record`, or `System.Text.Json`. JSON goes only through `DT_Tools.Core.Json`. Never build JSON by hand or escape strings manually.
- A blocking WebSocket wait MUST run on a non-Unity thread.
- BepInEx 5.4.20 quirks:
  - `ConfigEntryBase` has no `SettingChanged` event. Subscribe through EventInfo on the generic `ConfigEntry<T>`.
  - `JToken.Value<T>()` has an overload conflict. Use an explicit cast instead.
  - When a C# keyword is used as a member name, prefix it with `@`.
- Get user-data paths only from `DT_Tools/Core/DtPaths.cs`. Never build a path that contains `BepInEx/config`.

## Game version verification

- Before using any game API, verify in the game source that it exists, is accessible, and has the expected signature. This also applies to `typeof`, reflection strings, and Traverse member names.
- Record the verification in the commit message, not in code. If you cannot verify, write "unverified" in the commit message.
- When the game is upgraded, search for all Traverse and reflection strings and re-check each one.

## Coding conventions

- Log only through the `Log` facade, using the generic overloads: `Log.Info<T>(msg)`, `Log.Error<T>(msg)`, `Log.Warn<T>(ex, "context")`, `Log.Exception<T>(ex, "context")`.
- Forbidden: manual `[Tag]` prefixes, `Debug.Log`, custom buffers, bypassing the `Log` facade, and `"..." + ex.Message`.
- Use interpolated strings `$"..."`. Never concatenate literals and variables with `+`. An interpolated string is not a compile-time constant, so `const`, `case` labels, and attribute arguments accept only literals.
- Commands and automation run on the Unity main thread. HTTP threads MUST NOT call Unity APIs. Work that needs the main thread goes through `WebConsole.RunOnMain`.

## WebConsole

- Put API code in `DT_Tools/WebConsole/Api/`, one `*Api.cs` file per domain.
- Routes match by prefix in registration order. A new route MUST NOT start with an already registered, shorter route. Example: `/api/mp3/play` swallows `/api/mp3/playlist/*`.
- Run commands through `POST /api/run`. Send the session id in the `X-DT-Session` header.
- When `Password` is set, authentication is on. Failed requests return 401. WebSocket handshakes are authenticated too.
- Do not disable `RunInBackground`. If the game loses focus, the main thread stalls. A timeout on `/api/run` MUST be logged as a warning.
- Live logs use a WebSocket on a separate port (`WebConsoleOptions.WsPort`; 0 disables it). When the WebSocket is unavailable, fall back to polling `GET /api/log?since=N`. Unity Mono does not implement `HttpListener.AcceptWebSocketAsync`. Keep the probe, and do not warn on every failure.

## WebUI contract

- Source is in `webui-src/`. Build output goes to `DT_Tools/WebUI/`. Never edit build output by hand.
- Views MUST NOT call `fetch` directly or hard-code API paths. Use `src/api.js`.
- When inserting dynamic text into the DOM, use `textContent` or `esc()`.
- UI strings live in `src/i18n/messages.js`. Every key MUST fill all 9 columns: zh-CN, zh-TW, en, es, fr, ja, ko, pt-BR, ru. Use `{name}` for placeholders. Components call `t('key')`.
- Tag translation keys: `tag.label.<tagKey>`, `tag.desc.<tagKey>`, `tag.group.<groupKey>`.
- Local state lives only in browser localStorage. Never store it in backend config.
- A desktop widget goes in `src/desktop/`. Register it in the `widgets` array in `Desktop.vue`, and add an entry to the widget menu in the top bar.
- In tiled mode, the height comes from `--topbar-h` and `--dock-h` in `tokens.css`. If you change the Dock size, update `--dock-h` too.
- In a folder view, the `descOf` prop receives the item object, not the key. For summary text, use `configUtils.splitDesc`, not the first line of the raw text.

## MCP bridge

- Code lives in `DT_Tools/WebConsole/Mcp/`. Each tool has its own folder: `Mcp/Tools/<ToolName>/Tool.cs`. Tools are discovered by reflection, so no registration is needed.
- A tool class is a `sealed static class` marked `[McpTool("snake_case name", "description in Chinese", Author = "real name")]`. It provides `static JObject Schema()` and `static CommandResult Execute(JObject args)`.
- Implement actions as commands. The MCP tool `run_command` is the only entry point.
- Runtime code execution is forbidden. This includes Roslyn, script execution, and direct calls through a whitelist.
- A tool that touches Unity APIs MUST go through `WebConsole.RunOnMain`. Build response bodies with `JObject`. JSON goes only through `DT_Tools.Core.Json`.
- When the tool set changes, update `.github/skills/MCP/MCP.md` and `.github/skills/MCP/SKILL.md`. `MCP.md` is the reference for protocol packets. If it disagrees with the runtime, the output of the `packets` command wins, and you must update `MCP.md` to match.
