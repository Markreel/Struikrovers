using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Everything an action needs: who acts, the game state, the rules services
    /// (through Turn) and a way to report events.
    public sealed class ActionContext
    {
        public TurnManager Turn { get; }
        public Pawn Actor { get; }

        public GameState State => Turn.State;
        public BoardGrid Grid => Turn.State.Grid;

        /// True when an action already moved the turn flow on (rescue runs started),
        /// so the action phase must not finish the activation a second time.
        public bool FlowHandled { get; private set; }

        internal ActionContext(TurnManager turn, Pawn actor)
        {
            Turn = turn;
            Actor = actor;
        }

        public void Raise(TurnEvent turnEvent) => Turn.Raise(turnEvent);

        /// Each freed Royalty runs to safety, then the activation ends. Call this last.
        public void BeginRescueRuns(IEnumerable<Pawn> rescued)
        {
            FlowHandled = true;
            Turn.BeginRescueRuns(rescued, false);
        }

        public bool IsAdjacent(Vector2Int tile) => BoardGrid.IsAdjacent(Actor.Position, tile);

        public IEnumerable<Vector2Int> AdjacentTiles()
        {
            foreach (Vector2Int direction in BoardGrid.Directions)
            {
                Vector2Int p = Actor.Position + direction;
                if (Grid.InBounds(p))
                    yield return p;
            }
        }

        /// Checks the tile for a hidden enemy Bandit and applies what that does to the actor
        /// (see AmbushRules). Returns true when an ambush happened.
        public bool ResolveAmbush(Vector2Int tile)
        {
            AmbushResult result = Turn.AmbushRules.Resolve(Actor, tile);

            switch (result.Kind)
            {
                case AmbushKind.Abducted:
                    Raise(TurnEvent.Abducted(Actor, result.Bandit));
                    return true;

                case AmbushKind.SquireKilled:
                    Raise(TurnEvent.SquireKilled(Actor, result.Bandit, result.WoodStolen));
                    return true;

                case AmbushKind.Prevented:
                    // Equipment stopped it: the action carries on as if nobody was there.
                    Raise(TurnEvent.AmbushPrevented(Actor, result.PreventedBy));
                    return false;

                default:
                    return false;
            }
        }
    }
}