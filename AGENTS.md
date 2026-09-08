# Working on this project

A single-player FPS roguelike in Unity 6000.5.7f1, Built-in Render Pipeline, legacy Input
Manager. `README.md` describes what the systems *are*; this file describes how to change them
without breaking things.

Read `README.md` first, but verify what it says against the code — some sections have drifted
as the project has grown.

## The one thing to understand first

**The game is built entirely in code.** There are no prefabs, no materials, no meshes, and the
scene is optional: `GameBootstrap` spawns itself via `[RuntimeInitializeOnLoadMethod]`, so
pressing Play in an empty scene builds the whole game. Meshes come from `Build`, materials from
`MaterialLibrary`, sounds are synthesised by `Synth`.

This means almost nothing can be inspected by clicking on it. Enemies, familiars, projectiles
and pickups only exist while playing. The editor tools below exist because of this.

## Verifying a change

There is no play-mode test framework, so verification is compile plus tooling plus, for
anything about feel, a play session you cannot run yourself. Be explicit about which of those
you actually did.

### Unity batch mode

```
"E:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe" -batchmode -quit -nographics \
  -projectPath "E:/Projects/Wizard with a Gun" \
  -executeMethod Gunspire.EditorTools.EnemyTools.VerifyRoster \
  -logFile /path/to/log
```

Exit code 0 and no `error CS` in the log means it compiled. Any menu item can be driven this
way, which is how the rosters get checked without opening the editor.

The folder is still named after the old title. The game was renamed to Gunspire, but the
directory, the git remote and the repository name were left alone - renaming those is a
separate job with its own consequences, so the path above is deliberately not `Gunspire`.

**Check whether Unity is already open first.** It holds a project lock and batch mode will
refuse. `Get-Process -Name Unity` is the reliable check - a stale `Temp/UnityLockfile` with no
process is just leftover. If the editor is open, copy `Assets`, `Packages` and `ProjectSettings`
to a scratch directory and run there instead of disturbing the session. Delete
`Assets/Scripts/.vs` from the copy first; Visual Studio holds those files open.

### Menu items

Everything lives under **Gunspire** in the menu bar.

| Purpose | Items |
|---|---|
| Generate assets from the code rosters | Create **Weapon / Spell / Boon / Enemy / Familiar / Starting Loadout** Assets |
| Read a roster as a table | Log **Weapon Balance / Spell / Boon / Enemy / Familiar / Loadout / Sound** Table |
| Check things resolve | Verify Enemy Roster, Log Valid Ids, Log Missing Icons |
| One-off migration | Sync Alt Fires To Assets |

The Create items never overwrite an existing asset, so they are safe to re-run and will not
destroy Inspector tuning.

The Log tables exist because balancing one entry means comparing it against the others, which
reading object initializers one at a time does not let you do.

### Offline harness

Most content lives in plain C# classes that can be constructed outside Unity, so a stub
`UnityEngine` plus `dotnet build` catches a lot in about a second rather than a minute. Compiling
proves the code is well-typed; constructing the rosters proves they can actually be *built*,
which is a different question - an uninitialised `[SerializeReference]` list or an empty rarity
tier fails only at construction.

This harness is not currently in the repo. If you rebuild one, two lessons from the last one are
worth having:

- **Make the stubs real where the code depends on values.** `Mathf` returning its first argument
  and `Vector3.Dot` returning zero were fine until audio synthesis and movement maths needed
  them, at which point the tests agreed with themselves and proved nothing.
- **Carry Unity's deprecations into the stubs and treat warnings as errors.** `GetInstanceID` is
  obsolete in Unity 6; without that, the harness passed and the real compile failed.

## Traps

**Assets are the source of truth.** Guns, spells, boons, enemies, familiars and loadouts all use
the same hybrid: built-ins in code, any asset under a `Resources` folder merged over the top *by
id*. With the assets committed, editing `BuiltIn()` will look like it does nothing. Deleting the
asset is the way back to the code value.

**Adding a field to a definition needs a migration.** Existing assets deserialise it to its
default, so the value you wrote in code never appears. Regenerating the assets would fix it and
throw away Inspector tuning with it. `WeaponTools.SyncAltFires` is the pattern: copy only the new
field, only onto assets that do not have it. This has come up three times; a general version is
worth building if it comes up again.

**Ids are plain strings and the compiler does not check them.** A renamed gun does not fail to
build, it silently stops appearing. Tooling that resolves ids is the only safety net - add to it
rather than assuming.

**Coroutines die with their GameObject, and Unity does not unwind them.** Code after the last
`yield` never runs, and a `finally` will not save you. Anything a coroutine owns must own its own
lifetime instead - see `BeamVisual`.

**`[SerializeReference]` lists must be initialised at the declaration.** A collection initializer
calls `.Add` on null. This once took down the whole roster and, because the exception escaped
mid-`OnGUI`, presented as "the first card draws and clicking does nothing".

**`OnGUI` runs every frame.** Never roll random numbers or sample a roster there; decide once and
store it, or the cards reshuffle under the cursor.

**Unity 6 deprecations.** `FindObjectOfType` and `GetInstanceID` are gone. Use
`FindAnyObjectByType` / `FindObjectsByType(FindObjectsSortMode.None)`, and key dictionaries on the
object reference.

**A class named `Build` already exists.** Naming a method `Build` in the same namespace shadows
it and the error points somewhere unhelpful.

**Heredocs mangle apostrophes** in this shell. Use the Write tool for source files and commit
messages containing them.

## Conventions

Comments explain **why**, not what, and match the density of the surrounding code. A comment that
restates the line above it is noise; one that explains a non-obvious constraint, a trade-off, or
why the obvious approach was not taken is the reason the file is readable.

Player abilities, enemy attacks and familiar abilities are all `AbilityEffect` chains run through
`AbilityAttack` or `Spell.OnCast`. Adding an effect makes it available everywhere at once,
including in the Inspector's type picker. Prefer composing existing effects over writing new
behaviour.

Damage always resolves through `Health.TakeDamage`, and outgoing multipliers always come from
`Combat.OutgoingMultiplier`, so a boon is written once and applies to everything. `Health.Drain`
is the exception and exists precisely because a cost is not damage.

Physics layers are assigned by index in `Layers.cs` rather than named in ProjectSettings, so the
project needs no editor setup. Read that file before adding anything that needs to be hit.

## Committing

**Check `git status` before staging.** The project owner works in the editor between requests,
and their Inspector tuning shows up as modified assets. Sweeping those into a feature commit
buries a balance pass inside an unrelated change. Commit their work separately, say plainly in
the message that it is theirs, and do not guess at intent - if a change looks like a test rather
than a decision, leave it uncommitted and ask.

Commit messages explain the reasoning, not the diff. What was wrong, why this fix rather than the
obvious one, what was verified and what was not.
