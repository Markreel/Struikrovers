using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// All pawns and team resources. Turn flow joins this in step 5.
    public sealed class GameState
    {
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly TeamState[] teams;

        public BoardGrid Grid { get; }
        public IReadOnlyList<Pawn> Pawns => pawns;
        public IReadOnlyList<TeamState> Teams => teams;
        public int TeamCount => teams.Length;

        public GameState(BoardGrid grid, int teamCount)
        {
            Grid = grid;
            teams = new TeamState[teamCount];

            for (int team = 0; team < teamCount; team++)
            {
                teams[team] = new TeamState(team);

                Vector2Int castle = grid.GetCastlePosition(team);
                foreach (PawnType type in GameConfig.TurnOrder)
                    pawns.Add(new Pawn(grid, team, type, castle));
            }
        }

        // ---- Queries ----

        public Pawn Find(Predicate<Pawn> match) => pawns.Find(match);
        public List<Pawn> FindAll(Predicate<Pawn> match) => pawns.FindAll(match);
        public bool Exists(Predicate<Pawn> match) => pawns.Exists(match);

        public Pawn GetPawn(int team, PawnType type) =>
            pawns.Find(p => p.Team == team && p.Type == type);

        /// The pawn that acts in a turn slot. The Royalty slot falls back to the
        /// Bloodhound while the Royalty is abducted.
        public Pawn GetPawnForSlot(int team, PawnType slot)
        {
            Pawn pawn = GetPawn(team, slot);
            if (pawn == null && slot == PawnType.Royalty)
                pawn = GetPawn(team, PawnType.Bloodhound);
            return pawn;
        }

        public Pawn GetRoyaltyAt(Vector2Int position) =>
            pawns.Find(p => p.Type == PawnType.Royalty && !p.AtCastle && p.Position == position);

        public bool IsOccupied(Vector2Int position) =>
            pawns.Exists(p => !p.AtCastle && p.Position == position);

        /// The Bandit carrying this captive, or null.
        public Pawn GetCaptor(Pawn captive) =>
            pawns.Find(p => p.Type == PawnType.Bandit && p.HoldsCaptive(captive));

        public bool CanCarryMore(Pawn bandit) =>
            bandit != null && bandit.Type == PawnType.Bandit &&
            bandit.Captives.Count < GameConfig.MaxCaptivesPerBandit;

        // ---- Mutations that must keep the model consistent ----

        /// The Royalty becomes a Bloodhound (staying where it was) and the Bandit carries it.
        public bool TryCapture(Pawn royalty, Pawn bandit)
        {
            if (royalty == null || bandit == null) return false;
            if (royalty.Type != PawnType.Royalty || bandit.Type != PawnType.Bandit) return false;
            if (royalty.Team == bandit.Team) return false;
            if (!CanCarryMore(bandit)) return false;

            bandit.AddCaptive(royalty);
            royalty.Type = PawnType.Bloodhound;
            return true;
        }

        /// Bloodhound attack succeeded: the captive stands on the board again.
        public void FreeCaptive(Pawn bandit, Pawn captive, Vector2Int position)
        {
            if (!bandit.RemoveCaptive(captive)) return;

            captive.Type = PawnType.Royalty;
            captive.Position = position;
        }

        /// Bandit delivered the captive: the Royalty waits in its own castle.
        public void ReturnCaptiveHome(Pawn bandit, Pawn captive)
        {
            if (!bandit.RemoveCaptive(captive)) return;

            captive.Type = PawnType.Royalty;
            SendToCastle(captive);
        }

        public void SendToCastle(Pawn pawn)
        {
            pawn.Position = Grid.GetCastlePosition(pawn.Team);
        }
    }
}