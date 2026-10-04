# BCI Chess - Claude Code Project Instructions

## 1. Project Overview

We are building a chess game in Unity for a hackathon.

The player controls the chess game using a **Unicorn Hybrid Black EEG headset**.

The central feature is an adaptive BCI interaction system:

- Chess provides a set of possible actions.
- Only relevant/legal actions are presented as selectable BCI targets.
- Visual stimuli are displayed at different frequencies.
- EEG processing determines which stimulus the player is focusing on.
- The system dynamically assigns available frequencies to the current candidates.
- We cannot assume that enough reliably distinguishable frequencies exist to assign one unique frequency to every chess square.

The project must therefore minimize the number of simultaneous BCI choices.

The game should be visually polished enough for a hackathon demo while keeping the underlying architecture simple and maintainable.

---

# 2. IMPORTANT: Existing Unity Project

A Unity project has already been created from a template.

**Do not create a new Unity project.**

Before modifying anything:

1. Inspect the existing project structure.
2. Inspect the Unity version and packages.
3. Read/use the available Unity-specific Claude skill/plugin.
4. Determine what rendering pipeline and UI system the project already uses.
5. Reuse the existing project structure where practical.
6. Do not replace or restructure the project unnecessarily.

If an existing system can accomplish something cleanly, use it instead of introducing another dependency.

---

# 3. Development Philosophy

Build the game incrementally.

Do NOT attempt to implement the entire game in one giant change.

Work in the following order:

1. Project inspection
2. Chess board and chess rules
3. Mouse/keyboard chess interaction
4. BCI abstraction and simulated BCI
5. Adaptive BCI candidate selection
6. Visual stimulation system
7. Stockfish integration
8. Unicorn Hybrid Black integration
9. Polish/demo UX

After each major phase:

- Ensure the Unity project compiles.
- Fix errors before moving on.
- Keep the game playable.
- Avoid unnecessary rewrites.

If an implementation decision is unclear, prefer a simple implementation that can be replaced later.

---

# 4. Target User Experience

The player should be able to play a complete chess game against a computer.

The intended interaction is:

    Player turn
        ↓
    Determine legal moves
        ↓
    Determine selectable pieces
        ↓
    BCI selects a piece
        ↓
    Determine legal destinations
        ↓
    BCI selects destination
        ↓
    Execute move
        ↓
    Computer thinks
        ↓
    Stockfish chooses move
        ↓
    Execute computer move
        ↓
    Player turn

The player should not need to use a mouse during normal BCI gameplay.

Keyboard/mouse controls should exist for development and fallback purposes.

---

# 5. Chess Requirements

Implement standard chess.

The chess system must support:

- King
- Queen
- Rook
- Bishop
- Knight
- Pawn
- Legal movement
- Captures
- Check
- Checkmate
- Stalemate
- Castling
- En passant
- Pawn promotion
- Turn management
- Game-over detection

Draw rules should be implemented where practical, but they are lower priority than the core game.

Chess rules must be separated from the presentation/UI layer.

Do NOT put chess rules directly into board-rendering scripts.

---

# 6. Chess Architecture

Use a clean separation between chess state and Unity presentation.

A conceptual architecture is:

    ChessGame
        |
        +-- ChessBoard
        |
        +-- MoveGenerator
        |
        +-- ChessMove
        |
        +-- GameState

The exact class names may differ if the existing project or Unity skill suggests a better structure.

The important requirement is:

**Chess rules must not depend on the BCI system or Stockfish.**

The chess system should be usable independently with mouse/keyboard input.

---

# 7. Legal Move Generation

The chess system must expose a way to retrieve legal moves.

Conceptually:

    GetLegalMoves()

and:

    GetLegalMoves(piece)

For example, if the knight on g1 can move to:

    e2
    f3
    h3

the BCI system should receive exactly those three destinations.

The BCI system must never have to calculate chess legality itself.

---

# 8. BCI Interaction Concept

The main challenge of the project is BCI selection.

We cannot assume we have enough reliable stimulation frequencies for:

    A1
    A2
    A3
    ...
    H8

Therefore, **do NOT assign permanent frequencies to chess squares.**

Instead, frequencies are dynamically assigned to currently selectable candidates.

Example:

    Selectable pieces:

    Knight g1 → 10 Hz
    Bishop f1 → 12 Hz
    Pawn e2   → 14 Hz

After selecting Knight g1:

    e2 → 10 Hz
    f3 → 12 Hz
    h3 → 14 Hz

The frequency represents the current candidate only temporarily.

---

# 9. BCI Selection Architecture

Create an abstraction similar to:

    IBCISelector

The rest of the game must communicate with this abstraction rather than directly with Unicorn-specific code.

The interface should support concepts such as:

- Configure/selectable targets
- Assign stimulation frequencies
- Start selection
- Stop selection
- Receive selected target
- Reset/cancel selection

Do not over-engineer the interface.

The exact API should be determined after inspecting the available Unity/Unicorn integration.

---

# 10. BCI Target

Create a representation of a selectable BCI target.

A target should conceptually contain:

    Target ID
    Display information
    Associated frequency
    Optional chess reference

For chess, a target might represent:

    Piece on e2

or:

    Destination e4

The BCI system should not need to know whether a target represents a chess piece or a chess square.

---

# 11. Fake BCI / Development Mode

A simulated BCI implementation is REQUIRED.

Create something similar to:

    FakeBCISelector

This allows the entire game to be developed without the Unicorn headset.

The fake selector should use keyboard input.

For example:

    1 → select candidate 1
    2 → select candidate 2
    3 → select candidate 3
    ...

The fake implementation must use the exact same interface as the real BCI implementation.

This is extremely important because it allows the chess and BCI UI to be developed and tested before EEG integration.

---

# 12. Adaptive Candidate Selection

The BCI system should minimize the number of simultaneous choices.

At the start of a player's turn:

1. Get all legal moves.
2. Find all pieces that have at least one legal move.
3. Present those pieces as candidates.

Example:

    Legal moves:

    e2-e4
    e2-e3
    g1-f3
    g1-h3
    b1-a3
    b1-c3

Candidate pieces:

    e2
    g1
    b1

The player selects one.

Then:

    Selected piece = g1

Generate its legal moves:

    g1-f3
    g1-h3

The player now selects between:

    f3
    h3

Then the move is executed.

---

# 13. Automatic Selection

If there is only one candidate, do not require a BCI selection.

For example:

    Candidate count = 1

Automatically select it.

Likewise, if a selected piece has only one legal destination:

    g1 → f3

automatically select f3.

This reduces unnecessary interaction time.

---

# 14. Maximum Simultaneous BCI Targets

The number of simultaneously selectable targets must be configurable.

Create a configuration value such as:

    maxSimultaneousTargets

Do not assume a specific final value.

The default can be a small number suitable for development.

The system should support:

    Candidate count <= maxSimultaneousTargets

directly.

If:

    Candidate count > maxSimultaneousTargets

use hierarchical grouping.

---

# 15. Hierarchical Selection

If there are too many candidates for the available frequencies, divide them into groups.

Conceptually:

    20 candidates

        ↓

    Group A
    Group B
    Group C
    Group D

        ↓

    BCI selects Group B

        ↓

    Show candidates inside Group B

        ↓

    BCI selects candidate

The grouping algorithm should be deterministic and easy to understand.

Do not implement an overly complicated optimization algorithm unless it is actually needed.

The system should be designed so the grouping strategy can be replaced later.

---

# 16. Frequency Assignment

Frequency assignment must be centralized.

Create something similar to:

    FrequencyManager

It should receive a list of candidates and assign available frequencies.

Conceptually:

    candidates:
        A
        B
        C

    available frequencies:
        10 Hz
        12 Hz
        14 Hz

Result:

    A → 10 Hz
    B → 12 Hz
    C → 14 Hz

Do not hard-code frequencies into individual chess pieces or board squares.

Frequency configuration should be data-driven and easy to modify.

---

# 17. Frequency Separation

The exact frequencies will depend on the Unicorn Hybrid Black / EEG implementation and experimentation.

Do NOT invent scientifically unsupported frequency requirements.

Make the system configurable so we can later provide:

    frequency list

or:

    minimum frequency separation

or similar configuration.

The visual stimulation system must support changing frequencies without rewriting chess logic.

---

# 18. Visual Stimulation

The BCI targets must have a visually obvious stimulation effect.

The player should be able to tell:

- Which objects are currently selectable.
- Which frequency/stimulus belongs to which object.
- Which object is currently selected.
- When the system is waiting for BCI input.

The visual stimulation implementation should be independent from the chess piece renderer.

For example:

    ChessPieceRenderer

and:

    BCITargetVisual

should be separate concerns.

---

# 19. Chess Board UI

Create a clear chess board.

The board should support:

- 8x8 grid
- Chess pieces
- Piece movement
- Selection highlighting
- Legal move indication
- BCI target highlighting
- Current turn
- Check indication
- Game-over indication

The board should remain visually understandable even when BCI stimulation is active.

Avoid excessive visual effects that make the board difficult to read.

---

# 20. BCI UI State

The player should always understand what the system expects.

Possible states:

    WAITING_FOR_PLAYER

    SELECTING_PIECE

    SELECTING_DESTINATION

    PROCESSING_SELECTION

    COMPUTER_THINKING

    GAME_OVER

The UI should communicate the state clearly.

Example:

    "Select a piece"

then:

    "Select destination"

then:

    "Computer thinking..."

---

# 21. Stockfish

Use Stockfish as the computer opponent.

Stockfish should run independently from the Unity presentation layer.

Communication should use UCI where practical.

Conceptually:

    Unity
       |
       | UCI
       v
    Stockfish

Unity sends the current position.

Stockfish returns the computer's move.

Stockfish should NOT be responsible for:

- BCI candidate selection
- Frequency assignment
- Visual stimulation
- Unity rendering

---

# 22. Stockfish Client

Create a small abstraction similar to:

    StockfishClient

Responsibilities:

- Start Stockfish
- Communicate through UCI
- Send position
- Request move
- Parse best move
- Handle engine shutdown
- Handle errors/timeouts

Do not expose process-management details to the rest of the game.

The chess game should simply be able to request:

    GetBestMove(position)

or an equivalent asynchronous operation.

---

# 23. Stockfish Difficulty

The computer difficulty should be configurable.

Prefer a simple implementation initially.

Possible configuration:

    Search depth
    Thinking time
    Skill level

Do not spend significant development time on sophisticated difficulty scaling unless the basic game is already complete.

---

# 24. Unicorn Hybrid Black Integration

The Unicorn Hybrid Black implementation must be isolated.

Create something similar to:

    UnicornBCISelector

It should implement the same BCI interface used by:

    FakeBCISelector

The rest of the game should not need to know whether the selection came from:

    Keyboard
    Unicorn EEG
    Another BCI device

Do not make assumptions about the Unicorn SDK/API before inspecting the actual available integration/library.

If the Unicorn integration is not yet available:

1. Implement the abstraction.
2. Implement FakeBCISelector.
3. Leave the hardware adapter isolated.
4. Document exactly what remains to connect.

Do NOT create fake Unicorn API calls and pretend they work.

---

# 25. BCI Data Processing

If EEG processing is required, keep it separate from:

- Chess rules
- Chess rendering
- Stockfish
- Candidate generation

Conceptually:

    Unicorn EEG
        ↓
    EEG acquisition
        ↓
    Signal processing
        ↓
    Stimulus classification
        ↓
    BCI selection event
        ↓
    BCISelectionController

The chess game should only receive the final selection event.

---

# 26. Input Abstraction

Normal development should be possible without EEG hardware.

Support:

    Mouse
    Keyboard
    Fake BCI

The same chess selection pipeline should ideally be usable by all three.

For example:

    Mouse selection
         \
    Keyboard selection ----> SelectionController
         /
    BCI selection

This makes debugging much easier.

---

# 27. Testing

Create tests for important non-visual logic where practical.

At minimum, test:

### Chess

- Pawn movement
- Knight movement
- Bishop movement
- Rook movement
- Queen movement
- King movement
- Captures
- Check
- Checkmate
- Castling
- En passant
- Promotion

### BCI candidate generation

Given a chess position:

    GetSelectablePieces()

must return only pieces with legal moves.

Given a selected piece:

    GetSelectableDestinations(piece)

must return only legal destinations.

### Frequency assignment

Given:

    N candidates

and:

    N available frequencies

every candidate should receive exactly one frequency.

No two candidates should receive the same frequency within the same selection session.

### Candidate grouping

When:

    candidate count > maxSimultaneousTargets

the grouping system must produce valid groups containing every candidate exactly once.

---

# 28. Error Handling

The application should gracefully handle:

- Stockfish unavailable
- Stockfish process exits
- BCI unavailable
- BCI disconnect
- No BCI selection
- Invalid selection
- Selection timeout
- Game reset
- Application shutdown

The game should not crash because the Unicorn headset is disconnected.

---

# 29. Demo Mode

The hackathon demo must be possible without requiring the real BCI hardware.

Create a clear development/demo mode.

Example:

    BCI Mode:
        Simulated
        Unicorn

In simulated mode:

    Keyboard/mouse controls

In Unicorn mode:

    Real EEG selection

The rest of the application should behave identically.

---

# 30. Configuration

Avoid hard-coding values that we are likely to tune during the hackathon.

Configuration should include things such as:

    Available stimulation frequencies
    Maximum simultaneous BCI targets
    Selection timeout
    BCI confidence threshold
    Stockfish difficulty
    Board/UI settings

Use Unity-friendly configuration mechanisms where appropriate.

---

# 31. Performance

The game should remain responsive while:

- Rendering the board
- Running visual stimulation
- Receiving EEG data
- Processing BCI signals
- Running Stockfish

Do not block the Unity main thread with long-running operations.

Stockfish communication should not freeze the UI.

EEG acquisition/processing should not freeze the UI.

Use asynchronous/background processing where appropriate and safe.

---

# 32. Code Style

Prefer:

- Clear names
- Small classes
- Single responsibilities
- Simple data structures
- Explicit state
- Minimal coupling

Avoid:

- Giant MonoBehaviours
- Static global state everywhere
- Hard-coded chess rules inside UI
- Hard-coded BCI frequencies throughout the project
- Stockfish logic inside board rendering
- Unicorn-specific code scattered throughout the project
- Unnecessary dependencies
- Over-engineered abstractions

Follow the conventions already present in the template project where reasonable.

---

# 33. Important Architectural Rule

The following dependencies should be avoided:

    Chess → BCI
    Chess → Unicorn
    Chess → UI
    Stockfish → BCI
    Stockfish → UI
    BCI → Stockfish

Instead:

    Chess
      ↑
      |
    SelectionController
      |
      ↓
    BCI

and:

    Chess
      |
      ↓
    StockfishClient
      |
      ↓
    Stockfish

UI observes/presents the state of these systems.

---

# 34. Suggested High-Level Architecture

The resulting architecture should roughly resemble:

                         ┌─────────────────────┐
                         │      ChessGame      │
                         │                     │
                         │ Board / State /     │
                         │ Legal Moves         │
                         └─────────┬───────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
                    ▼                             ▼
          ┌──────────────────┐          ┌──────────────────┐
          │ BCI Selection    │          │ StockfishClient  │
          │ Controller       │          │                  │
          └────────┬─────────┘          └────────┬─────────┘
                   │                             │
                   ▼                             ▼
          ┌──────────────────┐             Stockfish
          │ IBCISelector     │
          └────────┬─────────┘
                   │
             ┌─────┴─────┐
             │           │
             ▼           ▼
       FakeBCI       UnicornBCI


          UI observes and presents the state
          of the above systems.