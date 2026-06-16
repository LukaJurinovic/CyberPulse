# CYBER-PULSE

> A rhythm-driven data-heist FPS where **the song is the level**.
> Unity 6000.4.0f1 · Universal Render Pipeline (URP 17) · PC (Windows)

---

## Overview

**CYBER-PULSE** is a fast-paced first-person shooter set inside a digital network. You play a rogue program surviving inside a hostile data cluster, hunted by geometric security constructs called **Trace Programs**.

The defining hook: **every run is generated from the loaded audio file.** The game analyses a song up front — BPM, duration, energy curve — and uses those values to seed the arena and schedule enemy waves on the track's energy peaks. Survive until the music ends and you win.

Rhythm is woven into combat. Shooting on the beat deals bonus damage; dash, reload, and weapon switch become instant on the beat; falling off-rhythm slows your movement and weapon handling, forcing a shift from aggression to defence until you recover your sync.

> *The song is the level. The beat is the resource.*

---

## Core Gameplay Loop

```
LOAD SONG → GENERATE LEVEL → SURVIVE THE TRACK → WIN / LOSE
```

1. **Song Load** — Pick a track (or let the game choose). It is analysed up front for BPM, duration, and an energy timeline, then the arena is built from that profile before the first bar plays.
2. **Survive the Track** — The song plays once in full. Enemy waves spawn at energy peaks detected in the pre-analysed curve.
3. **Win / Lose** — Reach the final note with the Trace Meter below 100% to win. If the Trace Meter ever hits 100%, you lose.

### The Trace Meter (your only health bar)

Player HP is effectively disabled — the **Trace Meter** is the sole survival mechanic. Keep it below 100% until the song ends.

| Source | Effect |
|---|---|
| Passive | +1.5%/s (pauses while no enemies are alive) |
| On-beat shot | Pauses passive fill for 4 beats |
| Enemy hits player | +8% per hit |
| Kill | −12% |
| Score milestone (every 500 pts) | −3% |
| Missile intercept | −5% |

---

## Rhythm Layer

The song's BPM defines a continuous beat window. Actions performed inside the on-beat window (15% either side of the beat) are enhanced; sustained off-rhythm play is penalised.

### On-beat bonuses

| Action | On-Beat | Off-Beat |
|---|---|---|
| Shoot | +50% damage | Normal |
| Dash | Cooldown resets instantly | Normal cooldown |
| Reload | Instant | Normal reload time |
| Weapon switch | Instant | 0.4s delay |
| Double jump | +40% air time | Normal |
| Kill | Large SYNC chunk (+25) | Small chunk (+5) |

### Off-rhythm penalty

Go 2 seconds without an on-beat action and movement drops to 70% and fire rate to 60% — pushing you toward defence until you re-sync. Any on-beat action clears the state instantly.

### SYNC Gauge (0–100)

Fills on on-beat actions, drains when off-rhythm or when taking damage. Spend it with right-click to trigger each weapon's **special attack**.

| Event | SYNC |
|---|---|
| On-beat shot | +8 |
| On-beat kill | +25 |
| On-beat dash | +5 |
| 2s without on-beat action | −1/s |
| Taking damage | −10 |

### Combo system

`ScoreManager` tracks a combo (up to x8) on a 5-second window: +1 for a same-weapon kill, +2 for a variety kill. Weapon specials add `50 × combo` score while in a combo.

---

## Weapons

All weapons share a common base; switching is via scroll wheel or keys 1/2/3. Specials are spent through the SYNC gauge.

| Weapon | Profile | Special (SYNC cost) |
|---|---|---|
| **Assault Rifle** | Hitscan, mag + reserve | 6-round rapid burst at 3× fire rate, zero spread (60) |
| **Revolver** | 6-round cylinder, high damage, slow | **Ricochet** — slow projectile bounces off up to 2 walls before detonating (50) |
| **Shotgun** | 8 pellets, wide spread, 2-round pump | Wide blast that knocks back and briefly grounds enemies; hops the player up (70) |

---

## Enemies

Geometric primitives rendered with a wireframe shader.

| Enemy | Shape | Behaviour | Counter-play |
|---|---|---|---|
| **Seeker** | Capsule | Patrol AI, melee attack | Standard combat |
| **Aerial Striker** | Sphere | Hovers, charges a telegraphed ground-circle column laser | Interrupt during charge (40% HP) or dash out of the circle |
| **Mirror Fighter** | Triangle | Mirrors player movement; 3 dashes then a 4s recharge; fires dodgeable projectiles | Bait its dashes, punish during recharge |
| **Homing Launcher** | Cylinder | Strafes at range, fires homing missiles that detonate on walls | Shoot missiles down; mind your cover |
| **Splitter** | Cube | Walks straight at you; on death splits into 4 fast one-hit small cubes | Kill in open space; clear the small cubes |

---

## Vertical Layered Progression

Levels are stacked into tiered arenas (default 3 layers). Clear a layer by **siphoning all its data nodes AND defeating all its enemies**, then ascend a ramp through a **locked door** that unlocks on clear and re-seals behind you so you can't drop back mid-fight. Clearing the top layer triggers the win state.
