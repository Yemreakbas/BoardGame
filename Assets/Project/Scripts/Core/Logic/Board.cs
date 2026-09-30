using System;
using BoardGame.Core.Pooling;

namespace BoardGame.Core.Logic
{
    /// <summary>Receives the cells cleared by one resolution step. The span is only valid during the call.</summary>
    public delegate void MatchedHandler(ReadOnlySpan<int> matchedIndices);

    /// <summary>
    /// Receives a shuffle as, for each cell, the index its piece came from. The span is only valid during the call.
    /// </summary>
    public delegate void ShuffledHandler(ReadOnlySpan<int> sourceIndices);

    /// <summary>
    /// Authoritative match-3 state: a flat, row-major array of piece IDs
    /// (index = y * Width + x, y = 0 is the bottom row, 0 is <see cref="Empty"/>).
    /// Owns the swap / match / collapse / refill rules and reports every change through events.
    /// Nothing allocates after the constructor returns.
    /// </summary>
    public sealed class Board
    {
        public const int Empty = 0;

        /// <summary>A swap was accepted: the pieces at (indexA, indexB) exchanged cells.</summary>
        public event Action<int, int> OnPiecesSwapped;

        /// <summary>A piece fell from (fromIndex) into the empty cell (toIndex); fromIndex is now empty.</summary>
        public event Action<int, int> OnPieceMoved;

        /// <summary>
        /// Cells cleared in one resolution step: runs plus everything specials hit. They are already
        /// <see cref="Empty"/> when this is raised.
        /// </summary>
        public event MatchedHandler OnMatched;

        /// <summary>
        /// A run of 4+ turned the piece at (index) into a special (pieceId, see <see cref="Piece"/>) in place,
        /// instead of clearing it. Raised just before the <see cref="OnMatched"/> of the same step.
        /// </summary>
        public event Action<int, int> OnSpecialCreated;

        /// <summary>A refill placed a new piece: (index, pieceId).</summary>
        public event Action<int, int> OnPieceSpawned;

        /// <summary>
        /// The settled board had no possible move, so its pieces were rearranged. If no shuffle of the same
        /// pieces worked, the board was regenerated and some piece IDs changed: read them back from the board.
        /// </summary>
        public event ShuffledHandler OnShuffled;

        /// <summary>Resolution of the last accepted swap finished: the board is stable and takes swaps again.</summary>
        public event Action OnSettled;

        /// <summary>
        /// Ice under (index) cracked: (index, layersLeft). Ice is a second layer fixed to the cell, under
        /// whatever piece is there; each clear of that cell removes one layer. Raised after the step's OnMatched.
        /// </summary>
        public event Action<int, int> OnIceChanged;

        /// <summary>Thickest ice a cell can hold.</summary>
        public const int MaxIceLayers = 2;

        /// <summary>
        /// A clear hit the locked piece at (index): the lock broke and the piece stays, now free. Raised just
        /// before the step's OnMatched (the index is not part of it).
        /// </summary>
        public event Action<int> OnPieceUnlocked;

        /// <summary>Shuffles tried before falling back to regenerating the board.</summary>
        private const int MaxShuffleAttempts = 100;

        private readonly int[] _cells;
        private readonly int[] _ice;            // ice layers per cell, 0 = none
        private readonly int[] _shuffleSources;
        private readonly BoardGenerator _generator;
        private bool _hasHoles; // Matches were cleared; the next step collapses and refills.
        private bool _shuffledThisResolve;
        private int _swapA;                 // cells of the last accepted swap
        private int _swapB;
        private bool _isFreshSwap;          // the next clear step is the swap's own, the only one that makes specials
        private bool _pendingBombSwap;      // the swap involved a color bomb; its clear step fires it

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _cells.Length;

        public Board(int width, int height, BoardGenerator generator)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

            int cellCount = checked(width * height);
            GlobalBuffer.EnsureCapacity(cellCount);

            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Width = width;
            Height = height;
            _cells = new int[cellCount];
            _ice = new int[cellCount];
            _shuffleSources = new int[cellCount];
            _generator.Fill(_cells, width, height);
            if (!FindPossibleMove(out _, out _)) Shuffle(); // Nobody listens yet, so this is silent.
        }

        public int ToIndex(int x, int y) => y * Width + x;
        public int ToX(int index) => index % Width;
        public int ToY(int index) => index / Width;
        public bool IsInside(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        public int GetPiece(int index) => _cells[index];
        public int GetPiece(int x, int y) => _cells[ToIndex(x, y)];

        /// <summary>Ice layers left under a cell (0 = none).</summary>
        public int GetIce(int index) => _ice[index];

        /// <summary>How many cells still have ice.</summary>
        public int IceCount { get; private set; }

        /// <summary>How many locked pieces are left.</summary>
        public int LockCount { get; private set; }

        public bool IsLocked(int index) => Piece.IsLocked(_cells[index]);

        /// <summary>Locks or frees the plain piece at a cell. Level setup only; specials cannot be locked.</summary>
        public void SetLocked(int index, bool locked)
        {
            if (IsResolving) throw new InvalidOperationException("Locks can only be set up on a stable board.");
            if ((uint)index >= (uint)_cells.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (Piece.SpecialOf(_cells[index]) != SpecialKind.None) throw new InvalidOperationException("Specials cannot be locked.");

            if (Piece.IsLocked(_cells[index]) == locked) return;
            _cells[index] = Piece.WithLock(_cells[index], locked);
            LockCount += locked ? 1 : -1;
        }

        /// <summary>
        /// Call after placing ice and locks: locks can leave the opening board without a single move, in
        /// which case it is shuffled silently (locked pieces keep their locks).
        /// </summary>
        public void FinishSetup()
        {
            if (!IsResolving && !FindPossibleMove(out _, out _)) Shuffle();
        }

        /// <summary>Lays ice under a cell. Level setup only: not allowed while the board is resolving.</summary>
        public void SetIce(int index, int layers)
        {
            if (IsResolving) throw new InvalidOperationException("Ice can only be set up on a stable board.");
            if ((uint)index >= (uint)_cells.Length) throw new ArgumentOutOfRangeException(nameof(index));

            layers = Math.Max(0, Math.Min(MaxIceLayers, layers));
            if (_ice[index] > 0) IceCount--;
            _ice[index] = layers;
            if (layers > 0) IceCount++;
        }

        /// <summary>Finds one swap that would create a match (e.g. for a hint). Only meaningful while not resolving.</summary>
        public bool FindPossibleMove(out int indexA, out int indexB)
        {
            return MatchDetector.FindPossibleMove(_cells, Width, Height, out indexA, out indexB);
        }

        /// <summary>
        /// True from an accepted <see cref="Swap"/> until <see cref="ResolveStep"/> reports the board stable.
        /// No swap is accepted meanwhile.
        /// </summary>
        public bool IsResolving { get; private set; }

        /// <summary>
        /// Swaps two orthogonally adjacent pieces if that creates a match or involves a color bomb. The board
        /// is then resolving: drive it with <see cref="ResolveStep"/> (one step per animation beat) or
        /// <see cref="ResolveAll"/>.
        /// </summary>
        /// <returns>
        /// True if the swap was accepted. A rejected swap (not adjacent, off the board, no match, or the
        /// board still resolving) leaves the board untouched and raises no event.
        /// </returns>
        public bool Swap(int x1, int y1, int x2, int y2)
        {
            if (IsResolving) return false;
            if (!IsInside(x1, y1) || !IsInside(x2, y2)) return false;
            if (Math.Abs(x1 - x2) + Math.Abs(y1 - y2) != 1) return false;

            int a = ToIndex(x1, y1);
            int b = ToIndex(x2, y2);
            if (!MatchDetector.IsAcceptedSwap(_cells, Width, Height, a, b)) return false;

            Exchange(a, b);
            _swapA = a;
            _swapB = b;
            _isFreshSwap = true;
            _pendingBombSwap = Piece.IsColorBomb(_cells[a]) || Piece.IsColorBomb(_cells[b]);

            IsResolving = true;
            OnPiecesSwapped?.Invoke(a, b);
            return true;
        }

        /// <summary>
        /// Advances resolution by one visible beat, alternating two kinds of step: clear every current match
        /// (<see cref="OnMatched"/>), then collapse and refill the holes (<see cref="OnPieceMoved"/>,
        /// <see cref="OnPieceSpawned"/>). Refills can create new matches, so cascades simply keep stepping.
        /// </summary>
        /// <returns>True if a step ran; false once the board is stable (and <see cref="IsResolving"/> is cleared).</returns>
        public bool ResolveStep()
        {
            if (!IsResolving) return false;

            if (_hasHoles)
            {
                for (int x = 0; x < Width; x++) RefillColumn(x, CollapseColumn(x));
                _hasHoles = false;
                return true;
            }

            // Detection runs here rather than reusing the swap check, so the shared match buffer is never
            // read across an event (listeners may use it too).
            bool bombSwap = _pendingBombSwap;
            _pendingBombSwap = false;
            int matchCount = MatchDetector.FindMatches(_cells, Width, Height);
            if (matchCount == 0 && !bombSwap)
            {
                // Settled. A dead board gets one shuffle step (never a second: a board too small to ever
                // have a move would otherwise shuffle forever).
                if (!_shuffledThisResolve && !FindPossibleMove(out _, out _))
                {
                    _shuffledThisResolve = true;
                    Shuffle();
                    OnShuffled?.Invoke(_shuffleSources.AsSpan(0, _cells.Length));
                    return true;
                }

                _shuffledThisResolve = false;
                IsResolving = false;
                OnSettled?.Invoke();
                return false;
            }

            ClearStep(bombSwap);
            _isFreshSwap = false;
            _hasHoles = true;
            return true;
        }

        /// <summary>Runs <see cref="ResolveStep"/> until the board is stable, for callers that do not animate.</summary>
        public void ResolveAll()
        {
            while (ResolveStep()) { }
        }

        // One clear step, starting from the cells MatchDetector.FindMatches just flagged:
        //  1. on the swap's own step, every run of 4+ turns one of its cells into a special (kept, not cleared);
        //  2. a color-bomb swap flags its targets;
        //  3. every flagged special fires, flagging more cells, until the chain ends;
        //  4. flagged cells are emptied: OnSpecialCreated for the new specials, then one OnMatched.
        private void ClearStep(bool bombSwap)
        {
            int cellCount = _cells.Length;
            bool[] flags = GlobalBuffer.MatchFlags;
            bool[] protectedCells = GlobalBuffer.ProtectedFlags;
            Array.Clear(protectedCells, 0, cellCount);

            // Specials form only from the player's own swap, never from cascades: on boards with few colors,
            // cascade-made specials refill faster than they fire and resolution would never end.
            int spawnCount = _isFreshSwap ? CreateSpecialsFromRuns(flags, protectedCells) : 0;
            if (bombSwap) FlagBombSwapTargets(flags, protectedCells);

            // Seed the chain with every special about to be cleared. Bombs that were swapped already did
            // their job in step 2 and must not fire again.
            int[] queue = GlobalBuffer.ActivationQueue;
            int queued = 0;
            for (int i = 0; i < cellCount; i++)
            {
                if (!flags[i] || Piece.SpecialOf(_cells[i]) == SpecialKind.None) continue;
                if (bombSwap && (i == _swapA || i == _swapB) && Piece.IsColorBomb(_cells[i])) continue;
                queue[queued++] = i;
            }
            for (int head = 0; head < queued; head++) queued = Fire(queue[head], flags, protectedCells, queued);

            int[] results = GlobalBuffer.MatchResultIndices;
            int count = 0, cracked = 0, unlocked = 0;
            for (int i = 0; i < cellCount; i++)
            {
                if (!flags[i]) continue;
                cracked = CrackIce(i, cracked);

                if (Piece.IsLocked(_cells[i]))
                {
                    // A hit on a locked piece only breaks the lock; the piece stays and is free from now on.
                    _cells[i] = Piece.WithLock(_cells[i], false);
                    LockCount--;
                    GlobalBuffer.Unlocked[unlocked++] = i;
                    continue;
                }

                results[count++] = i;
                _cells[i] = Empty;
            }
            for (int u = 0; u < unlocked; u++) OnPieceUnlocked?.Invoke(GlobalBuffer.Unlocked[u]);

            for (int s = 0; s < spawnCount; s++)
            {
                int index = GlobalBuffer.SpawnIndices[s];
                int piece = GlobalBuffer.SpawnPieces[s];
                _cells[index] = piece;
                cracked = CrackIce(index, cracked); // it was part of the match, so the ice under it breaks too
                OnSpecialCreated?.Invoke(index, piece);
            }
            OnMatched?.Invoke(results.AsSpan(0, count));

            int[] crackedCells = GlobalBuffer.IceCracked;
            for (int c = 0; c < cracked; c++) OnIceChanged?.Invoke(crackedCells[c], _ice[crackedCells[c]]);
        }

        // Removes one ice layer under `index`, recording the cell for OnIceChanged. Returns the new count.
        private int CrackIce(int index, int cracked)
        {
            if (_ice[index] == 0) return cracked;
            if (--_ice[index] == 0) IceCount--;
            GlobalBuffer.IceCracked[cracked] = index;
            return cracked + 1;
        }

        // A run of 5+ makes a color bomb, a run of 4 a rocket along the run. Returns how many were queued
        // in GlobalBuffer.SpawnIndices / SpawnPieces; their cells are protected and unflagged.
        private int CreateSpecialsFromRuns(bool[] flags, bool[] protectedCells)
        {
            int spawnCount = 0;
            for (int run = 0; run < GlobalBuffer.RunCount; run++)
            {
                int length = GlobalBuffer.RunLength[run];
                if (length < 4) continue;

                int cell = PickSpawnCell(GlobalBuffer.RunStart[run], length, GlobalBuffer.RunStride[run], protectedCells);
                if (cell < 0) continue;

                SpecialKind kind = length >= 5 ? SpecialKind.ColorBomb
                    : GlobalBuffer.RunIsRow[run] ? SpecialKind.RowRocket : SpecialKind.ColumnRocket;
                int color = kind == SpecialKind.ColorBomb ? Empty : Piece.ColorOf(_cells[cell]);

                protectedCells[cell] = true;
                flags[cell] = false;
                GlobalBuffer.SpawnIndices[spawnCount] = cell;
                GlobalBuffer.SpawnPieces[spawnCount] = Piece.Make(color, kind);
                spawnCount++;
            }
            return spawnCount;
        }

        // Where the player moved a piece if that cell is in the run, otherwise the run's middle, otherwise any
        // cell; never a cell already holding a special (it would vanish without firing) or already reserved.
        private int PickSpawnCell(int start, int length, int stride, bool[] protectedCells)
        {
            for (int j = 0; j < length; j++)
            {
                int cell = start + j * stride;
                if ((cell == _swapA || cell == _swapB) && CanHoldNewSpecial(cell, protectedCells)) return cell;
            }

            int middle = start + (length - 1) / 2 * stride;
            if (CanHoldNewSpecial(middle, protectedCells)) return middle;

            for (int j = 0; j < length; j++)
            {
                int cell = start + j * stride;
                if (CanHoldNewSpecial(cell, protectedCells)) return cell;
            }
            return -1;
        }

        private bool CanHoldNewSpecial(int cell, bool[] protectedCells)
        {
            return !protectedCells[cell] && Piece.SpecialOf(_cells[cell]) == SpecialKind.None && !Piece.IsLocked(_cells[cell]);
        }

        // A bomb swapped with a piece clears that piece's color; two bombs clear the whole board.
        private void FlagBombSwapTargets(bool[] flags, bool[] protectedCells)
        {
            int a = _cells[_swapA];
            int b = _cells[_swapB];
            bool both = Piece.IsColorBomb(a) && Piece.IsColorBomb(b);
            int color = Piece.IsColorBomb(a) ? Piece.ColorOf(b) : Piece.ColorOf(a);

            if (!protectedCells[_swapA]) flags[_swapA] = true;
            if (!protectedCells[_swapB]) flags[_swapB] = true;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (protectedCells[i] || _cells[i] == Empty) continue;
                if (both || Piece.ColorOf(_cells[i]) == color) flags[i] = true;
            }
        }

        // Fires the special at `index`, flagging the cells it hits and queueing any special among them.
        // Returns the new queue length.
        private int Fire(int index, bool[] flags, bool[] protectedCells, int queued)
        {
            switch (Piece.SpecialOf(_cells[index]))
            {
                case SpecialKind.RowRocket:
                {
                    int rowStart = ToY(index) * Width;
                    for (int x = 0; x < Width; x++) queued = FlagHit(rowStart + x, flags, protectedCells, queued);
                    break;
                }
                case SpecialKind.ColumnRocket:
                {
                    for (int y = 0; y < Height; y++) queued = FlagHit(ToX(index) + y * Width, flags, protectedCells, queued);
                    break;
                }
                case SpecialKind.ColorBomb:
                {
                    // Hit by a rocket, a bomb takes out the most common color left on the board.
                    int color = MostCommonUnflaggedColor(flags, protectedCells);
                    if (color == Empty) break;
                    for (int i = 0; i < _cells.Length; i++)
                    {
                        if (Piece.ColorOf(_cells[i]) == color) queued = FlagHit(i, flags, protectedCells, queued);
                    }
                    break;
                }
            }
            return queued;
        }

        private int FlagHit(int index, bool[] flags, bool[] protectedCells, int queued)
        {
            if (flags[index] || protectedCells[index] || _cells[index] == Empty) return queued;
            flags[index] = true;
            if (Piece.SpecialOf(_cells[index]) != SpecialKind.None) GlobalBuffer.ActivationQueue[queued++] = index;
            return queued;
        }

        private int MostCommonUnflaggedColor(bool[] flags, bool[] protectedCells)
        {
            int[] counts = GlobalBuffer.ColorCounts;
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < _cells.Length; i++)
            {
                if (!flags[i] && !protectedCells[i]) counts[Piece.ColorOf(_cells[i])]++;
            }
            counts[Empty] = 0; // empty cells and color bombs are colorless

            int best = Empty;
            for (int color = 1; color < counts.Length; color++)
            {
                if (counts[color] > counts[best]) best = color;
            }
            return best;
        }

        // Moves the column's pieces down over the empty cells below them, keeping their order.
        // Returns the lowest row left empty, which is where the refill starts.
        private int CollapseColumn(int x)
        {
            int writeY = 0;
            for (int y = 0; y < Height; y++)
            {
                int from = ToIndex(x, y);
                int piece = _cells[from];
                if (piece == Empty) continue;

                if (y != writeY)
                {
                    int to = ToIndex(x, writeY);
                    _cells[to] = piece;
                    _cells[from] = Empty;
                    OnPieceMoved?.Invoke(from, to);
                }
                writeY++;
            }
            return writeY;
        }

        // Spawns bottom-up, so listeners receive a column's new pieces in stacking order.
        private void RefillColumn(int x, int firstEmptyY)
        {
            for (int y = firstEmptyY; y < Height; y++)
            {
                int index = ToIndex(x, y);
                int piece = _generator.NextPiece();
                _cells[index] = piece;
                OnPieceSpawned?.Invoke(index, piece);
            }
        }

        // Rearranges the pieces into a layout with no match and at least one move, recording in
        // _shuffleSources where each cell's piece came from. Falls back to fresh boards (which never start
        // with a match) if shuffling keeps failing, e.g. when one piece type dominates the board.
        private void Shuffle()
        {
            int cellCount = _cells.Length;
            for (int i = 0; i < cellCount; i++) _shuffleSources[i] = i;

            for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
            {
                _generator.Shuffle(_cells, _shuffleSources, cellCount);
                if (MatchDetector.FindMatches(_cells, Width, Height) == 0 && FindPossibleMove(out _, out _)) return;
            }

            // Fresh boards carry no locks (e.g. too many locked pieces left no playable arrangement): the locks
            // are gone, so the count must follow.
            LockCount = 0;
            for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
            {
                _generator.Fill(_cells, Width, Height);
                if (FindPossibleMove(out _, out _)) return;
            }
            // Still no move: the board is too small to ever have one. Leave the last match-free fill.
        }

        private void Exchange(int a, int b)
        {
            int piece = _cells[a];
            _cells[a] = _cells[b];
            _cells[b] = piece;
        }
    }
}
