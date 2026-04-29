---
layout: default
title: Publishing
nav_order: 4
has_children: true
description: "How to package and release a game on Steam using NEShim."
---

# Publishing guide

There are two ways to ship a game with NEShim. Choose the path that fits your situation:

---

## [Pre-built release](publishing-prebuilt)

Download a packaged NEShim binary, drop in your ROM and assets, and configure `config.json`. No compiler or .NET SDK required.

Use this path when you want to get a game out quickly and do not need a custom exe icon or a private HMAC key.

---

## [Building from source](publishing-source)

Clone the repository, customise the project, and build your own binary.

Use this path when you need a custom exe icon embedded in the file, want to rename the underlying assembly (`NEShim.dll` → `MyGame.dll`), or need to rotate the HMAC key before a public release.
