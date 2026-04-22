# Security Policy

## Supported versions

Only the latest release distributed through Steam is supported with security fixes.
Older versions are not patched.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Report privately by email: **neshim.github@gmail.com**

Include:
- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof-of-concept
- The version of the application where you observed it (check the Steam build ID)

You can expect an acknowledgement within **72 hours** and a status update within
**7 days**. If a fix is warranted, a patched release will be pushed to Steam before
any public disclosure.

## Scope

In scope:
- Remote code execution via crafted ROM, save-state, or configuration files
- Supply chain vulnerabilities in compiled dependencies shipped with the application
- Vulnerabilities that allow privilege escalation or sandbox escape on the host system

Out of scope:
- Cheat tools, memory editors, and game-state manipulation for personal use
- Issues only reproducible on unsupported or modified versions
- Steam platform issues (report those to Valve)
