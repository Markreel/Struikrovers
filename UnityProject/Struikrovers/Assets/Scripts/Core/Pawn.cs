using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public sealed class Pawn
    {
        private readonly BoardGrid grid;
        private readonly List<Pawn> captives = new List<Pawn>();
        private readonly List<IPawnAction> actions = new List<IPawnAction>();
        private readonly List<object> actionSources = new List<object>();
        private Vector2Int position;
        /// For a Bandit: where it stood at the start of its latest activation, but only
        /// if it was already carrying a captive then. Used by the Shrieking Chalice.
        public Vector2Int? CarryStartPosition { get; internal set; }

        public int Team { get; }

        /// What this pawn currently is. A Royalty becomes a Bloodhound while abducted.
        public PawnType Type { get; internal set; }

        /// What this pawn was created as. Never changes (Royalty stays Royalty).
        public PawnType BaseType { get; }

        /// Equipment belongs to the pawn, whatever it currently is.
        public PawnEquipment Equipment { get; } = new PawnEquipment();

        /// Moving a pawn always ends its exposure.
        public Vector2Int Position
        {
            get => position;
            set
            {
                if (value == position)
                    return;

                position = value;
                IsExposed = false;
            }
        }

        /// A Bandit whose tree was chopped by its own team. It is visible to everyone
        /// until it moves away.
        public bool IsExposed { get; internal set; }

        /// True while the pawn stands on its team's castle tile (drawn in the frame, not on the board).
        public bool AtCastle => grid.IsCastle(Position);

        /// For a Bandit: the Royalty pawns it carries. A carried Royalty is currently
        /// a Bloodhound standing at its own Position. The view draws a CAPTIVE token for
        /// it on the captor's tile.
        public IReadOnlyList<Pawn> Captives => captives;
        public bool IsCarrying => captives.Count > 0;
        public bool HoldsCaptive(Pawn pawn) => captives.Contains(pawn);

        /// What this pawn can do in its action phase.
        public IReadOnlyList<IPawnAction> Actions => actions;

        public Pawn(BoardGrid grid, int team, PawnType type, Vector2Int position)
        {
            this.grid = grid;
            Team = team;
            Type = type;
            BaseType = type;
            this.position = position;
        }

        /// Adds an action. The source tags where it came from (a card, a piece of
        /// equipment, ActionCatalog.BaseSource) so RevokeAll can remove exactly that.
        public void Grant(IPawnAction action, object source = null)
        {
            actions.Add(action);
            actionSources.Add(source);
        }

        /// Removes every action that was granted by this source. Returns how many.
        public int RevokeAll(object source)
        {
            int removed = 0;

            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(actionSources[i], source))
                    continue;

                actions.RemoveAt(i);
                actionSources.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        internal void AddCaptive(Pawn pawn) => captives.Add(pawn);
        internal bool RemoveCaptive(Pawn pawn) => captives.Remove(pawn);
    }
}