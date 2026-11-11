# soundboards/

Drop audio files here. They show up in `openmod soundboard list` and (soon) the
Soundboard page in the app.

**Supported:** `.wav` `.mp3` `.ogg` `.flac` `.m4a` `.aac`
**Not supported:** `.opus` — symphonia 0.5 has no opus decoder yet.

Everything is downmixed to mono at play time, because the voice pipeline is mono.

## Lazy by design

| Step | Cost | What it reads |
|------|------|---------------|
| `scan` | one `readdir` | file names, formats, sizes — **no file is opened** |
| `probe` | header only | container frame count → duration |
| `load` | full decode | every packet → mono f32 |

That's why 500 clips doesn't slow startup down, and why `list` shows `—` for
durations until you ask:

```bash
openmod soundboard list                      # instant, names only
openmod soundboard list --probe              # + durations from headers
openmod soundboard play airhorn --vmic       # decode + play into the virtual mic
openmod soundboard play horn.mp3             # full file name when a stem is ambiguous
```

Durations can't always be known without decoding (a VBR mp3 with no Xing header
stores no frame count). Those clips stay `—` and are decoded on play instead of
being guessed at.
