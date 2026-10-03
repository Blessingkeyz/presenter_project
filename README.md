<div align="center">
  <img src="BiblePresenter.App/Resources/AppIcon.png" width="96" alt="Beacon Worship logo" />

  # Beacon Worship

  A lightweight Windows presentation console for live worship services - scripture, songs, and announcements, run from a laptop with nothing more than integrated graphics.
</div>

---

## Why "Beacon Worship"

The app started as a Bible verse presenter, but it grew into something a single-purpose name couldn't cover: scripture, song lyrics, custom announcement slides, and whole ordered services, all driven from one live console. "Beacon Worship" describes what the tool actually does - it's the light that guides a room through a service, not just a page-turner for one book.

## The logo

<img src="BiblePresenter.App/Resources/AppIcon.png" width="64" align="left" style="margin-right:16px" />

The mark is called **Signal** - a beacon sending its light outward in every direction. Of the three concepts explored, it was the most literal reading of "beacon" itself, and the clearest at small sizes (it has to work as a 16px title-bar icon as much as a 256px badge).

Its colors aren't arbitrary - they're the app's own design tokens: **Candle** gold (`#D9A441`) on **Ink** (`#1C1815`), the exact palette used throughout the live console and the projector output. The logo isn't a separate brand asset bolted onto the app; it's drawn from the same system the UI already uses.

<br clear="left" />

## Features

- **Bible import** - drop in Zefania, OSIS, or generic XML Bible files; format is auto-detected
- **Fast search** - reference lookup (`gen 2 2-10` works without the colon), fuzzy/typo-tolerant keyword search, and ambiguous book-stem matching (`thess` finds both epistles)
- **Sets** - OpenSong-compatible, ordered service plans mixing Scripture, Song, Custom, and Image items, stored as real files in your `Sets` folder. Reorder by dragging, add or remove items, and changes save automatically as you work
- **Image items** - a picture (such as an event flyer) shown full screen on the projector
- **Song library** - search by title or by lyrics content, with a live lyrics preview before you commit
- **Create and edit songs** - build a new song from labelled sections (V1, V2, C, B...), duplicate a section (the copy takes the next free number), and reorder sections by dragging. Editing a song offers three saves: **Save** (overwrite the library file), **Save as new song**, or **Save for this set only**
- **Hymn and chorded files** - numbered lines group into verses across stanzas, and chord rows are read but not shown on the projector
- **Per-style formatting** - independent font, size, weight, color, border, shadow, and background for Scripture vs. Song/Slide content, all configurable in Settings. The text auto-sizes to fit the screen, and the outline stays in proportion to the text
- **Characters per slide** - set separately for Scripture and for Song/Slide in Settings. A long verse or stanza continues on the next slide, cut at a natural pause (a line break, sentence, or comma where possible)
- **Alignment** - Scripture is left-aligned; songs and custom slides are centred
- **Live console** - a mini preview shows what's about to go live before you commit, Go Live / Present opens the projector window, Stop closes it, and Next/Prev automatically advance through a whole Set
- **Dual-window output** - a control window for the operator, a clean borderless window for the projector, positioned on whichever display you choose
- **Safe saving** - only what you changed is written. An unchanged set or song file is left byte-for-byte as it was, and editing a song keeps its other fields (copyright, presentation order, and so on)
- **Runs on integrated graphics** - text is rendered as vector geometry rather than GPU shader effects, so it stays smooth on the kind of hardware a church laptop actually has

## Where your files live

| What | Location |
|---|---|
| Song library (OpenSong song files) | `%USERPROFILE%\Songs` |
| Sets (OpenSong set files) | `%USERPROFILE%\Sets` |
| Settings | `%AppData%\BiblePresenter\settings.json` |
| Imported backgrounds | `%AppData%\BiblePresenter\Media` |
| Images used in Image items | `%AppData%\BiblePresenter\SetImages` |

Songs and sets are plain files, so OpenSong and other tools can read them alongside this app.

## Tech

WPF / .NET 8, MVVM with a hand-rolled `ObservableObject`/`RelayCommand` (no external MVVM framework). Chosen deliberately over Avalonia, MAUI, or Electron-style alternatives for the lightest possible footprint on weak GPUs - see the project's own design notes for the reasoning.

## Getting started

```
dotnet build
dotnet run --project BiblePresenter.App
```

Tests: `dotnet test BiblePresenter.Tests`

---

<div align="center">

Designed and built by **Blessingkeyz**

</div>
