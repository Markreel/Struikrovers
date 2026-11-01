using UnityEngine;

namespace Struikrovers.Core
{
    /// The Royalty is abducted. Not possible when the Bandit is already carrying a captive.
    public sealed class AbductionAmbush : IAmbushResponse
    {
        private readonly GameState state;

        public AbductionAmbush(GameState state) { this.state = state; }

        public AmbushResult Respond(Pawn victim, Pawn bandit)
        {
            if (!state.TryCapture(victim, bandit))
                return default;

            return new AmbushResult(AmbushKind.Abducted, bandit);
        }
    }

    /// The Squire is killed and goes back to its castle. The Bandit steals wood and equipment.
    public sealed class SquireKillAmbush : IAmbushResponse
    {
        private readonly GameState state;
        private readonly CardSystem cards;

        public SquireKillAmbush(GameState state, CardSystem cards)
        {
            this.state = state;
            this.cards = cards;
        }

        public AmbushResult Respond(Pawn victim, Pawn bandit)
        {
            TeamState victimTeam = state.Teams[victim.Team];
            int stolen = Mathf.Min(GameConfig.WoodStolenOnSquireKill, victimTeam.Wood);
            victimTeam.Wood -= stolen;
            state.Teams[bandit.Team].Wood += stolen;

            // The Bandit takes 1 of the Squire's equipment (everything with Royal Inheritance).
            cards.QueueSteal(bandit, victim);

            state.SendToCastle(victim);
            return new AmbushResult(AmbushKind.SquireKilled, bandit, stolen);
        }
    }
}