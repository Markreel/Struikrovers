using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public sealed class Pawn
    {
        private readonly BoardGrid grid;
        private readonly List<Pawn> captives = new List<Pawn>();

        public int Team { get; }

        /// Only GameState changes this (abduct / free / deliver).
        public PawnType Type { get; internal set; }

        public Vector2Int Position { get; set; }

        /// True while the pawn stands on its team's castle tile (drawn in the frame, not on the board).
        public bool AtCastle => grid.IsCastle(Position);

        /// For a Bandit: the Royalty pawns it carries. A carried Royalty is currently
        /// a Bloodhound standing at its own Position. The view draws a CAPTIVE token for
        /// it on the captor's tile.
        public IReadOnlyList<Pawn> Captives => captives;
        public bool IsCarrying => captives.Count > 0;
        public bool HoldsCaptive(Pawn pawn) => captives.Contains(pawn);

        public Pawn(BoardGrid grid, int team, PawnType type, Vector2Int position)
        {
            this.grid = grid;
            Team = team;
            Type = type;
            Position = position;
        }

        internal void AddCaptive(Pawn pawn) => captives.Add(pawn);
        internal bool RemoveCaptive(Pawn pawn) => captives.Remove(pawn);
    }
}