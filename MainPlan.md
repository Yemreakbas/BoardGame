# Role & Context
You are a Lead Gameplay Engineer at a top-tier mobile puzzle studio. I have already created the folder structure in my Unity project. Your task is to write the C# scripts for a **Zero-Allocation, Pure C# Match-3 Logic Core** coupled with a clean **Unity View Layer**.

# Strict Technical Constraints (CRITICAL)
1. **Zero Garbage Collection (GC):** After initial setup (`Awake`/`Start`), you MUST NOT use the `new` keyword. No dynamic allocations (no `List<T>`, no `new int[]`, no closures/lambdas) during gameplay.
2. **No LINQ:** `System.Linq` is strictly forbidden.
3. **Pre-allocated Buffers:** Use a static/global array buffer for returning match results.
4. **Data Locality (1D Array):** The board state MUST be a single 1D array (`int[]` for piece IDs, 0 = empty). Coordinate math: `index = y * width + x`.
5. **MVC Separation:** The `Core` namespace must NOT contain `using UnityEngine;`. Use standard C# `Action` or `delegate` events to communicate with the View.

# Required Scripts
Please provide the complete C# code for the following 6 files. Use clear, professional English.

### Core/Pooling/
1. **`GlobalBuffer.cs`**: Static class with pre-allocated arrays (e.g., `public static int[] MatchResultIndices`) to store match data without creating new lists.

### Core/Logic/
2. **`Board.cs`**: Holds the 1D array state, C# events (`OnPieceMoved`, `OnMatched`), and a `Swap(x1, y1, x2, y2)` method.
3. **`MatchDetector.cs`**: Horizontal/vertical match algorithm writing directly into `GlobalBuffer.MatchResultIndices` and returning the match count.
4. **`BoardGenerator.cs`**: Fills the board initially ensuring no pre-existing matches.

### View/ (Unity Layer)
5. **`PieceView.cs`**: MonoBehaviour. Visual representation. Includes a method stub for moving to a target position.
6. **`BoardView.cs`**: MonoBehaviour. Instantiates pieces ONLY at the start. Listens to `Board.cs` events to update visuals. Captures simulated swipe input to call `Board.Swap`.

Please provide all 6 scripts. If the response is too long, provide the `Core` scripts first and wait for my prompt to generate the `View` scripts.