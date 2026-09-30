using System;

namespace BoardGame.Core.Logic
{
    public enum LevelOutcome
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>
    /// Level rules: within <see cref="MoveLimit"/> moves, reach <see cref="TargetScore"/> and break all the
    /// ice on the board (levels without ice only need the score). The outcome is decided only when the board
    /// settles, so the cascades of the last move still count. Allocation-free once constructed.
    /// </summary>
    public sealed class LevelState : IDisposable
    {
        /// <summary>The level was won or lost. Read <see cref="Outcome"/>.</summary>
        public event Action OnOutcomeChanged;

        private readonly Board _board;
        private readonly ScoreKeeper _score;

        public int MoveLimit { get; }
        public int TargetScore { get; }
        public LevelOutcome Outcome { get; private set; }

        public int MovesLeft => Math.Max(0, MoveLimit - _score.Moves);

        /// <summary>True while the player may still attempt a swap.</summary>
        public bool CanMove => Outcome == LevelOutcome.Playing && MovesLeft > 0;

        /// <remarks>Construct after <paramref name="score"/>, so its handlers run first on shared events.</remarks>
        public LevelState(Board board, ScoreKeeper score, int moveLimit, int targetScore)
        {
            if (moveLimit <= 0) throw new ArgumentOutOfRangeException(nameof(moveLimit), moveLimit, "Move limit must be positive.");
            if (targetScore <= 0) throw new ArgumentOutOfRangeException(nameof(targetScore), targetScore, "Target score must be positive.");

            _board = board ?? throw new ArgumentNullException(nameof(board));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            MoveLimit = moveLimit;
            TargetScore = targetScore;
            _board.OnSettled += HandleSettled;
        }

        public void Dispose()
        {
            _board.OnSettled -= HandleSettled;
        }

        private void HandleSettled()
        {
            if (Outcome != LevelOutcome.Playing) return;

            if (_score.Score >= TargetScore && _board.IceCount == 0) Outcome = LevelOutcome.Won;
            else if (MovesLeft == 0) Outcome = LevelOutcome.Lost;
            else return;

            OnOutcomeChanged?.Invoke();
        }
    }
}
