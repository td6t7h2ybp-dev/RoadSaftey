# Voice-over recording script: child voice, Modern Standard Arabic (US 5.1)

DoD: every instruction is **spoken in Modern Standard Arabic (no dialect)** and the voice is **a child's**.
The lines in the game today are synthetic placeholders; record a real child (with the parents' written
agreement) and replace the files. **Same file name = used automatically**, no code to change.

## Recording settings

- Folder: `Assets/Audio/Resources/Audio/VO/`
- Format: **WAV, mono, 44 100 Hz, 16 bit**, about 0.3 s of silence before and after.
- Quiet room, phone or USB mic 20 cm from the mouth, no music.
- Friendly, slow, clear; fusha pronunciation (no Tunisian words or accent shortcuts). Read it with the child first.
- Keep the old file if a take is not good: Git keeps history, but do not commit a half-finished set.

## Lines (text is in `Assets/Scripts/Core/GameMessages.cs`)

| # | File | Arabic text (read as one sentence) | When it plays |
|---|---|---|---|
| 1 | `VO_Seatbelt_ar.wav` | ضع حزام الأمان قبل أن تنطلق! | Go pressed without the belt |
| 2 | `vo_belt_done.wav` | أحسنت! حزام الأمان يحميك | Belt on |
| 3 | `vo_collision.wav` | انتبه، انظر قبل أن تتقدّم! | Crash |
| 4 | `vo_light_red.wav` | الضوء أحمر، توقّف قبل الخط! | Approaching the red light |
| 5 | `vo_light_green.wav` | الضوء أخضر، يمكنك الانطلاق! | Light turns green after waiting |
| 6 | `vo_ran_red.wav` | توقّف! لا نمرّ عندما يكون الضوء أحمر | Red light run |
| 7 | `vo_well_done_red.wav` | أحسنت! انتظرت الضوء الأخضر | Waited for green |
| 8 | `vo_ped_waiting.wav` | طفل يريد أن يعبر، توقّف ليمرّ | Kid waiting at the zebra crossing |
| 9 | `vo_ped_violation.wav` | قف! اترك المشاة يعبرون أوّلًا | Did not stop for the kid |
| 10 | `vo_thanks.wav` | شكرًا! أنت سائق لطيف | Kid crossed, thanks the driver |
| 11 | `vo_kids.wav` | انتبه! أطفال يلعبون، خفّف السرعة | "Children" sign before the ball |
| 12 | `vo_slowed.wav` | أحسنت! خفّفت السرعة في الوقت المناسب | Slowed down before the ball |
| 13 | `vo_ball.wav` | انتبه للكرة! | Ball, too fast |
| 14 | `vo_finish.wav` | وصلت بالسلامة! | Finish line |
| 15 | `vo_wrong_lane.wav` | انتبه! سيّارة أمامك | Wrong lane / car ahead |
| 16 | `vo_try_again.wav` | لا بأس! لنحاول مرة أخرى | Last heart lost |
| 17 | `vo_near_miss.wav` | انتبه! كدت تصدم شخصًا | **New** (Semaine 9): almost hit a pedestrian, no file yet |

`oldman_remark.wav` is the old man's own voice (an adult character), keep it adult.

## Check after recording

Press Play in `Main.unity` and trigger each situation (or use the debug keys), and check: the child's
voice plays, the words match the bubble, and the volume matches the other lines.
