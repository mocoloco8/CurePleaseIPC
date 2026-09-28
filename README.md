## Game connection (cortana)

Cure Please no longer uses EliteAPI or its own CurePlease_addon. It talks to the Windower 4 **cortana** addon through `CortanaIPC.dll`.

1. Install the addon from its canonical source — https://github.com/mocoloco8/CortanaIPC-Windower-Lua — into
   `Windower4/addons/cortana`. That is the copy that is maintained; every app sharing the addon
   (Cure Please included) needs the current one, and an older copy silently drops feeds Cure Please
   depends on: `SAB` (known abilities — without it no job ability ever fires) and `CASTRES`
   (action outcomes, in `combat.lua`).
2. In each game client, run `//lua load cortana` (or add it to your Windower init script).
3. Start Cure Please and pick the PL and the monitored character as before.

The `WindowerAddon/cortana` folder in this repo is a stale snapshot kept only for reference — do not
install it over a newer one.

Start and pause Cure Please from its own window. There are no in-game commands, and the "Enable Hot Keys" option is disabled.

Requires .NET Framework 4.8. Ashita is not supported.

Cure-Please
===========

Open source repository for anyone wishing to make changes to the official Cure Please source code. (Discontinued, see link below for an alternative version)

https://github.com/h1pp0/Cure-Please
