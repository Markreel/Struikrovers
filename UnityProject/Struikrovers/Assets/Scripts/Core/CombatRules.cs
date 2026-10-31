using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public readonly struct FightResult
    {
        public readonly Pawn Attacker;
        public readonly Pawn Defender;
        /// The tile the fight took place on (the defender's tile).
        public readonly Vector2Int Position;
        /// Final totals, including modifiers, of the roll that decided the fight.
        public readonly int AttackerRoll;
        public readonly int DefenderRoll;
        public readonly int Rerolls;
        public readonly bool AttackerWon;
        /// Squire fights: wood the winner took.
        public readonly int WoodStolen;
        /// Bandit vs Bandit: captives the winner took over from the loser.
        public readonly int CaptivesTakenOver;
        /// Captives that were freed instead (a losing Bandit's captives).
        public readonly IReadOnlyList<Pawn> Rescued;
        public readonly int Blocked;

        public Pawn Winner => AttackerWon ? Attacker : Defender;
        public Pawn Loser => AttackerWon ? Defender : Attacker;
        public int ReleasedCount => Rescued?.Count ?? 0;

        public FightResult(
            Pawn attacker, Pawn defender, Vector2Int position,
            int attackerRoll, int defenderRoll, int rerolls, bool attackerWon,
            int woodStolen, int captivesTakenOver, IReadOnlyList<Pawn> rescued, int blocked)
        {
            Attacker = attacker;
            Defender = defender;
            Position = position;
            AttackerRoll = attackerRoll;
            DefenderRoll = defenderRoll;
            Rerolls = rerolls;
            AttackerWon = attackerWon;
            WoodStolen = woodStolen;
            CaptivesTakenOver = captivesTakenOver;
            Rescued = rescued;
            Blocked = blocked;
        }
    }

    /// Squire vs Squire, Bandit vs Squire and Bandit vs Bandit. Both sides roll the die and
    /// add modifiers, the highest total wins, and a tie is rolled again.
    /// The loser dies and goes to its castle.
    public sealed class CombatRules
    {
        private readonly GameState state;
        private readonly RescueRules rescue;
        private readonly Func<int> rollDie;

        public CombatRules(GameState state, RescueRules rescue, Func<int> rollDie)
        {
            this.state = state;
            this.rescue = rescue;
            this.rollDie = rollDie;
        }

        /// All modifiers for one fighter. Equipment bonuses (+1 / +2) get added here later.
        public int GetModifier(Pawn pawn, Pawn opponent, bool isAttacker)
        {
            int modifier = 0;

            if (isAttacker)
            {
                if (pawn.Type == PawnType.Squire)
                    modifier += GameConfig.SquireAttackerBonus;
                else if (pawn.Type == PawnType.Bandit)
                    modifier += opponent.Type == PawnType.Squire
                        ? GameConfig.BanditAttackerBonusVsSquire
                        : GameConfig.BanditAttackerBonus;
            }

            // A Bandit that is busy abducting fights worse, as attacker or defender.
            if (pawn.Type == PawnType.Bandit && pawn.IsCarrying)
                modifier += GameConfig.BanditAbductingModifier;

            return modifier;
        }

        // ---- Fighting Squires (Squire or Bandit attacker) ----

        private static bool CanFightSquires(Pawn pawn) =>
            pawn != null &&
            (pawn.Type == PawnType.Squire || pawn.Type == PawnType.Bandit);

        /// Tiles next to this Squire or Bandit that hold an enemy Squire.
        public HashSet<Vector2Int> GetSquireTargets(Pawn attacker)
        {
            var targets = new HashSet<Vector2Int>();

            if (!CanFightSquires(attacker) || attacker.AtCastle)
                return targets;

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Type == PawnType.Squire && !pawn.AtCastle &&
                    pawn.Team != attacker.Team &&
                    BoardGrid.IsAdjacent(attacker.Position, pawn.Position))
                {
                    targets.Add(pawn.Position);
                }
            }

            return targets;
        }

        /// Fights every enemy Squire on the tile, one after another, until the attacker dies.
        public List<FightResult> FightSquiresAt(Pawn attacker, Vector2Int tile)
        {
            var results = new List<FightResult>();

            if (!CanFightSquires(attacker))
                return results;

            List<Pawn> opponents = state.FindAll(p =>
                p.Type == PawnType.Squire && !p.AtCastle &&
                p.Position == tile && p.Team != attacker.Team);

            foreach (Pawn opponent in opponents)
            {
                FightResult result = FightSquires(attacker, opponent);
                results.Add(result);

                // The attacker lost and is back in its castle.
                if (!result.AttackerWon)
                    break;
            }

            return results;
        }

        /// The defender is a Squire. The attacker is a Squire or a Bandit.
        public FightResult FightSquires(Pawn attacker, Pawn defender)
        {
            Vector2Int spot = defender.Position;
            bool attackerWins = Roll(attacker, defender, out int attackerTotal, out int defenderTotal, out int rerolls);

            Pawn winner = attackerWins ? attacker : defender;
            Pawn loser = attackerWins ? defender : attacker;
            Vector2Int loserSpot = loser.Position;

            TeamState loserTeam = state.Teams[loser.Team];
            int stolen = Mathf.Min(GameConfig.WoodStolenOnSquireFight, loserTeam.Wood);
            loserTeam.Wood -= stolen;
            state.Teams[winner.Team].Wood += stolen;

            // Hook for later: the winner takes 1 piece of the loser's equipment of choice.

            // A losing Bandit drops its captives. They are freed and run to safety.
            var rescued = new List<Pawn>();
            int blocked = 0;

            if (loser.Type == PawnType.Bandit && loser.IsCarrying)
                blocked = rescue.ReleaseCaptives(loser, loserSpot, new HashSet<Vector2Int>(), rescued);

            state.SendToCastle(loser);

            return new FightResult(
                attacker, defender, spot, attackerTotal, defenderTotal, rerolls,
                attackerWins, stolen, 0, rescued, blocked);
        }

        // ---- Bandit vs Bandit ----

        /// A Bandit that ended its move on a bush or tree fights the Bandit already there.
        public bool TryFightOccupant(Pawn arriving, out FightResult result)
        {
            result = default;

            if (arriving == null || arriving.Type != PawnType.Bandit || arriving.AtCastle)
                return false;

            Pawn occupant = state.GetBanditAt(arriving.Position, arriving);
            if (occupant == null || occupant.Team == arriving.Team)
                return false;

            result = FightBandits(arriving, occupant);
            return true;
        }

        public FightResult FightBandits(Pawn attacker, Pawn defender)
        {
            Vector2Int spot = defender.Position;
            bool attackerWins = Roll(attacker, defender, out int attackerTotal, out int defenderTotal, out int rerolls);

            Pawn winner = attackerWins ? attacker : defender;
            Pawn loser = attackerWins ? defender : attacker;

            // The winner takes over the loser's captives if it has room. Anything left
            // is freed and runs to safety, same as after a Bloodhound attack.
            int takenOver = 0;
            foreach (Pawn captive in new List<Pawn>(loser.Captives))
                if (state.TransferCaptive(loser, winner, captive))
                    takenOver++;

            var rescued = new List<Pawn>();
            int blocked = 0;

            if (loser.IsCarrying)
                blocked = rescue.ReleaseCaptives(loser, spot, new HashSet<Vector2Int>(), rescued);

            // Hook for later: the winner steals 1 piece of the loser's equipment of choice.

            state.SendToCastle(loser);

            return new FightResult(
                attacker, defender, spot, attackerTotal, defenderTotal, rerolls,
                attackerWins, 0, takenOver, rescued, blocked);
        }

        // ---- Dice ----

        /// Both sides roll; ties are rolled again. Returns true when the attacker wins.
        private bool Roll(
            Pawn attacker, Pawn defender,
            out int attackerTotal, out int defenderTotal, out int rerolls)
        {
            int attackerModifier = GetModifier(attacker, defender, true);
            int defenderModifier = GetModifier(defender, attacker, false);

            attackerTotal = 0;
            defenderTotal = 0;
            rerolls = 0;

            while (true)
            {
                attackerTotal = rollDie() + attackerModifier;
                defenderTotal = rollDie() + defenderModifier;

                if (attackerTotal != defenderTotal || rerolls >= GameConfig.MaxFightRerolls)
                    break;

                rerolls++;
            }

            return attackerTotal > defenderTotal;
        }
    }
}