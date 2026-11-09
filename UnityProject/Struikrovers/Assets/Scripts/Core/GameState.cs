using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// All pawns and team resources.
    public sealed class GameState
    {
        private readonly List<Pawn> pawns = new List<Pawn>();
        private readonly TeamState[] teams;

        public BoardGrid Grid { get; }
        public IReadOnlyList<Pawn> Pawns => pawns;
        public IReadOnlyList<TeamState> Teams => teams;
        public int TeamCount => teams.Length;

        private readonly Dictionary<int, Vector2Int> chaliceSpots = new Dictionary<int, Vector2Int>();

        /// Shrieking Chalice: per team, the tile where its Royalty was abducted. Public.
        public IReadOnlyDictionary<int, Vector2Int> ChaliceSpots => chaliceSpots;
        private readonly HashSet<int> chaliceTeams = new HashSet<int>();

        public GameState(BoardGrid grid, int teamCount)
        {
            Grid = grid;
            teams = new TeamState[teamCount];

            for (int team = 0; team < teamCount; team++)
            {
                teams[team] = new TeamState(team);
                Vector2Int castle = grid.GetCastlePosition(team);

                foreach (PawnType type in GameConfig.TurnOrder)
                {
                    var pawn = new Pawn(grid, team, type, castle);

                    foreach (IPawnAction action in ActionCatalog.GetDefaults(type))
                        pawn.Grant(action, ActionCatalog.BaseSource);

                    pawns.Add(pawn);
                }
            }
        }

        // ---- Queries ----

        public Pawn Find(Predicate<Pawn> match) => pawns.Find(match);
        public List<Pawn> FindAll(Predicate<Pawn> match) => pawns.FindAll(match);
        public bool Exists(Predicate<Pawn> match) => pawns.Exists(match);

        public Pawn GetPawn(int team, PawnType type) =>
            pawns.Find(p => p.Team == team && p.Type == type);

        /// The team's pawn of this role (Royalty, Squire, Bandit), even while the Royalty is a Bloodhound.
        public Pawn GetPawnByRole(int team, PawnType role) =>
            pawns.Find(p => p.Team == team && p.BaseType == role);

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

        /// The Bandit standing on a board tile. A hiding place holds at most one Bandit.
        public Pawn GetBanditAt(Vector2Int position, Pawn except = null) =>
            pawns.Find(p =>
                p.Type == PawnType.Bandit && !p.AtCastle &&
                p.Position == position && p != except);

        /// Tiles a Bloodhound may attack or a Squire may stab: every bush or tree,
        /// plus any tile where a Bandit stands exposed (a stump after its tree was chopped).
        public bool IsAttackable(Vector2Int position)
        {
            if (!Grid.InBounds(position))
                return false;

            if (Grid.GetTerrain(position).IsHidingPlace())
                return true;

            return pawns.Exists(p =>
                p.Type == PawnType.Bandit && p.IsExposed && !p.AtCastle && p.Position == position);
        }

        public bool IsOccupied(Vector2Int position) =>
            pawns.Exists(p => !p.AtCastle && p.Position == position);

        /// True when a pawn of another team stands on this board tile.
        public bool HasEnemyPawnAt(Vector2Int position, int team) =>
            pawns.Exists(p => !p.AtCastle && p.Team != team && p.Position == position);

        /// True when a Squire of another team stands on this board tile.
        public bool HasEnemySquireAt(Vector2Int position, int team) =>
            pawns.Exists(p =>
                p.Type == PawnType.Squire && !p.AtCastle &&
                p.Team != team && p.Position == position);

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

            // Decide now: once the Royalty is a Bloodhound its equipment is ignored.
            if (EquipmentEffects.MarksAbductionSpot(royalty)) chaliceTeams.Add(royalty.Team);
            else chaliceTeams.Remove(royalty.Team);
            chaliceSpots.Remove(royalty.Team);   // nothing is shown at abduction

            royalty.Type = PawnType.Bloodhound;
            return true;
        }

        /// A Bandit that wins a fight takes over the loser's captive. Not possible when the
        /// winner has no room, or when the captive is the winner's own Royalty (that one is freed).
        public bool TransferCaptive(Pawn from, Pawn to, Pawn captive)
        {
            if (from == null || to == null || captive == null) return false;
            if (!from.HoldsCaptive(captive)) return false;
            if (captive.Team == to.Team) return false;
            if (!CanCarryMore(to)) return false;

            from.RemoveCaptive(captive);
            to.AddCaptive(captive);
            chaliceSpots.Remove(captive.Team);   // the new captor has no recorded start yet
            return true;
        }

        /// The captive stands on the board again, as a Royalty.
        public void FreeCaptive(Pawn bandit, Pawn captive, Vector2Int position)
        {
            if (!bandit.RemoveCaptive(captive)) return;

            chaliceSpots.Remove(captive.Team);
            chaliceTeams.Remove(captive.Team);
            captive.Type = PawnType.Royalty;
            captive.Position = position;
        }

        /// Bandit delivered the captive: the Royalty waits in its own castle.
        public void ReturnCaptiveHome(Pawn bandit, Pawn captive)
        {
            if (!bandit.RemoveCaptive(captive)) return;

            chaliceSpots.Remove(captive.Team);
            chaliceTeams.Remove(captive.Team);
            captive.Type = PawnType.Royalty;
            SendToCastle(captive);
        }

        public void SendToCastle(Pawn pawn)
        {
            pawn.Position = Grid.GetCastlePosition(pawn.Team);
        }

        /// Called when a Bloodhound's turn starts. Moves the Chalice marker to the captor's
        /// previous tile. Returns that tile, or null when there is nothing to show.
        public Vector2Int? RefreshChalice(Pawn bloodhound)
        {
            int team = bloodhound.Team;
            chaliceSpots.Remove(team);

            if (bloodhound.Type != PawnType.Bloodhound || !chaliceTeams.Contains(team))
                return null;

            Vector2Int? spot = GetCaptor(bloodhound)?.CarryStartPosition;
            if (spot == null || Grid.IsCastle(spot.Value))
                return null;

            chaliceSpots[team] = spot.Value;
            return spot;
        }
    }
}