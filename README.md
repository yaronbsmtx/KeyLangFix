# KeyLangFix

KeyLangFix is a small Windows tray utility that fixes text typed on the wrong keyboard layout. If your keyboards are English and Hebrew and you start typing Hebrew words while the English layout is active (or the other way round), KeyLangFix notices, switches the layout, and rewrites the text you just typed in the correct language.

It works in any text box in any application, because it watches keystrokes rather than reading the contents of a particular control.

It runs fully offline: there is no network code, no telemetry and no update check.

## Features

- Detects wrong-layout typing per word, using small statistical language models embedded in the program. There is no AI or machine-learning model and nothing is downloaded.
- Switches the keyboard layout only when there is enough evidence (a minimum number of letters and a confidence margin you can adjust).
- Rewrites the already-typed text in the right language, so you do not have to retype it.
- Plays a short sound when it switches. The volume is adjustable.
- Lives in the notification area. Hovering over the icon shows the version and statistics: switches in total, this week and today.
- Follows the Windows light/dark setting for its settings window.
- Windows 10 and 11, 64-bit.

Supported languages: English and Hebrew. Other languages can be added (see [Adding a language](#adding-a-language)).

## Using it

Run `KeyLangFix.exe`. An icon appears in the notification area (it may be under the "^" overflow arrow). It starts enabled.

Right-click the icon:

| Item | What it does |
|---|---|
| Disable / Enable | Turns detection off or on. A grey icon means disabled. |
| Settings... | Opens the settings window. Double-clicking the icon does the same. |
| Reset statistics... | Clears the switch counters, after a confirmation. |
| Quit | Exits the program. |

### Settings

| Setting | Meaning |
|---|---|
| Confidence | How much stronger the evidence for the other layout must be before switching. Levels run from Very low (switches eagerly, more mistakes) to Very high (switches rarely, almost no mistakes). The default is Medium. |
| Minimum letters | How many letters of a run must be typed before a switch is allowed (3 to 8, default 4). |
| Rewrite the text already typed | When on, the text typed before the switch is deleted and retyped in the correct language. When off, only the layout changes. |
| Play a sound | Plays a short tone on every automatic switch. |
| Sound volume | 0 to 100%. Releasing the slider plays a preview. |
| Start with Windows | Adds or removes a per-user startup entry (no administrator rights needed). |
| Defaults | Puts the factory defaults into the window. Nothing is saved until you press OK. |

If you switch the layout back by hand within a few seconds of an automatic switch, KeyLangFix treats that as a wrong guess and asks for slightly more evidence for the next few minutes.

### Where data is stored

Everything is under `%APPDATA%\KeyLangFix`:

- `settings.ini` - your settings
- `stats.ini` - switch counters (the first-launch date, the total, and daily counts for the last two weeks)

Typed text is never saved. The program keeps only the last few words in memory, and clears them when you click, move the cursor, change window or press a shortcut.

## How it works

1. A global low-level keyboard hook receives each key press. The program does not read the contents of any text box.
2. For each key it computes what character the key gives on the current layout and on the other installed layouts that have a language model.
3. A character-trigram model and a list of common words for each language are used to compare "this word in the current layout" with "this word in the other layout". The words are scored letter by letter, and the other layout must win by the confidence margin.
4. If so, the program asks the active window to change layout and then, once the window has actually switched, replays the same physical keys so the text appears in the new layout.

Some details that affect behaviour:

- A word that is a real word in the current layout, or still the beginning of one, is never switched. For example, `bang` typed on the English layout is also the Hebrew word `נשמע`, so it is left alone.
- Hebrew final-form letters (ך ם ן ף ץ) never appear in the middle of a word, so a reading that puts one mid-word is rejected. This avoids switching on things like `e.g` or web addresses.
- Short words such as `i do` before `not know` are included in the rewrite once the following words make the switch certain.

## Limitations

- Windows does not let a program see the keystrokes of an application running as administrator unless it is also running as administrator. KeyLangFix does not do that, so it will not act in elevated windows.
- Password boxes are skipped when they use the standard Windows edit control. Other kinds of password fields cannot be recognised.
- Only the text typed since the last click, cursor movement or window change is rewritten (at most about 48 characters). It does not read or rewrite the rest of the text box.
- A few applications ignore the request to change layout, and applications with autocorrect or an input-method editor may treat the rewrite differently.
- The statistical models are approximate. On the project's own word lists, at the default confidence, about 84 to 86 percent of words typed on the wrong layout are caught, and about 0.3 to 0.6 percent of correctly typed words are switched by mistake. Real typing will differ.

## Building

Requirements: Windows, the .NET 9 SDK.

```powershell
dotnet test KeyLangFix.slnx
dotnet publish src/KeyLangFix -c Release -o dist
```

This produces a self-contained, trimmed single-file `dist\KeyLangFix.exe` that does not need .NET installed.

A Native AOT build is also configured:

```powershell
dotnet publish src/KeyLangFix -c Release -p:Aot=true -o dist
```

It needs the Visual Studio "Desktop development with C++" build tools, and it has not been built on the development machine, so treat it as untested.

### Regenerating assets and models

The language models and the icon and sound are generated by Python scripts. This is only needed when you change them, and it is the only step that uses the network (to install the packages).

```powershell
pip install wordfreq pillow
python tools/build_ngrams.py    # writes src/KeyLangFix/Resources/en.ngm and he.ngm, plus test data
python tools/make_assets.py     # writes the icons and switch.wav
```

`build_ngrams.py` uses the open `wordfreq` word lists and removes common junk tokens before building the models.

## Project layout

```
KeyLangFix.slnx
src/KeyLangFix/
  Program.cs                 entry point, message loop, tray command handling
  Core/Engine.cs             key handling, detection trigger, layout switch, rewrite
  Detection/                 language models, detector, thresholds
  Input/WordBuffer.cs        the recent keys, per layout
  Hooks/HookManager.cs       keyboard and mouse hooks, hook watchdog
  Native/NativeMethods.cs    Win32 declarations
  Tray/TrayIcon.cs           notification-area icon, tooltip and menu
  Ui/                        settings window and theme support
  Services/                  settings, statistics, sound, start with Windows
  Resources/                 language models, icons, sound (embedded)
tests/KeyLangFix.Tests/      unit tests for the detector, buffer and statistics
tools/                       scripts that generate models and assets
```

## Adding a language

1. Add the language to `LANGS` in `tools/build_ngrams.py` (language id and alphabet), and a cleaning rule in `acceptable()` if its word list has junk tokens.
2. Run the script, then add the resulting file name to `Files` in `src/KeyLangFix/Detection/Models.cs`.
3. If the language has one-letter words, extend `IsSingleLetterWord` in `NgramModel.cs`.
4. Add detector tests with example words typed on the wrong layout.

## Troubleshooting

Key logging exists only in Debug builds (`dotnet build -c Debug`); it is compiled out of Release and published builds. To see what a Debug build sees, create an empty file named `debug.on` in `%APPDATA%\KeyLangFix`, then restart the program. It then writes `debug.log` next to it with one line per key press and the detector's decisions.

**The log records what you type in every application. Treat it as sensitive, and delete both files when you are finished.** (Setting the environment variable `KEYLANGFIX_TEST=1` also turns on the log, and additionally lets the program react to synthetic keystrokes, which the automated tests use.)

Running with `--settings` opens the settings window at start-up.

## Status

Version 1.0.0. No licence has been chosen yet.
