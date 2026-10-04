Put the Stockfish executable here as:

    Assets/StreamingAssets/Stockfish/stockfish.exe

Download: https://stockfishchess.org/download/  (Windows, x86-64 build; rename the .exe to stockfish.exe)

The path can be changed on the ChessGame object (Computer > Stockfish Path); relative paths are
resolved against Assets/StreamingAssets, absolute paths are used as-is.

If the executable is missing or fails, the game continues with a simple built-in opponent
(Computer > Use Fallback Opponent).

Stockfish is GPLv3 licensed; if you distribute a build that bundles it, include its license and source link.
