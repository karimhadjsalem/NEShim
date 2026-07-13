---
layout: default
title: Publishing
nav_order: 4
parent: Pre-release
has_children: true
description: "How to package and release a game on Steam using NEShim."
---

# Publishing guide

There are two ways to ship a game with NEShim. Choose the path that fits your situation:

---

## [Pre-built release](publishing-prebuilt)

Download a packaged NEShim binary, drop in your ROM and assets, and configure `config.json`. No compiler or .NET SDK required.

Use this path when you want to get a game out quickly and do not need a custom exe icon or the signing key baked into the binary.

---

## [Building from source](publishing-source)

Clone the repository, customise the project, and build your own binary.

Use this path when you need a custom exe icon embedded in the file, want to rename the underlying assembly (`NEShim.dll` → `MyGame.dll`), or want the signing public key compiled into the binary rather than read from `config.json`.

---

## [Multi-game / DLC](multi-game)

An *additional* step layered on top of either path above — not a third alternative to them. You still build/publish the engine binary via the pre-built or source path exactly as described there; multi-game mode adds a `games/multigame.json` manifest plus one independently-packaged `games/<gameId>/` content folder per game, each shipped as a separate Steam DLC depot.

Use this when you're shipping a curated collection of games from one engine binary, selected at runtime via a front-end carousel, rather than one game per publish.
