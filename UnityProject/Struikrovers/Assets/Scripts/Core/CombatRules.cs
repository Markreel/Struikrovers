using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The dice of one fight: each side's dice and modifier, and how many ties were rolled again.
    public readonly struct FightRoll
    {
        public readonly DiceRoll AttackerDice;
        public readonly DiceRoll DefenderDice;
        public readonly int AttackerModifier;
        public readonly int DefenderModifier;
        public readonly int Rerolls;

        /// The sum of the dice, without modifiers.
        public int AttackerDie => AttackerDice != null ? AttackerDice.Total : 0;
        public int DefenderDie => DefenderDice != null ? DefenderDice.Total : 0;
        public int AttackerTotal => AttackerDie + AttackerModifier;
        public int DefenderTotal => DefenderDie + DefenderModifier;
        public bool AttackerWon => AttackerTotal > DefenderTotal;

        public FightRoll(
            DiceRoll attackerDice, DiceRoll defenderDice,
            int attackerModifier, int defenderModifier, int rerolls)
        {
            AttackerDice = attackerDice;
            DefenderDice = defenderDice;
            AttackerModifier = attackerModifier;
            DefenderModifier = defenderModifier;
            Rerolls = rerolls;
        }
    }

    public readonly struct FightResult
    {
        public readonly Pawn Attacker;
        public readonly Pawn Defender;
        /// The tile the fight took place on (the defender's tile).
        public readonly Vector2Int Position;
        public readonly FightRoll Roll;
        /// Squire fights: wood the winner took.
        public readonly int WoodStolen;
        /// Bandit vs Bandit: captives the winner took over from the loser.
        public readonly int CaptivesTakenOver;
        /// Captives that were freed instead (a losing Bandit's captives).
        public readonly IReadOnlyList<Pawn> Rescued;
        public readonly int Blocked;

        public int AttackerRoll => Roll.AttackerTotal;
        public int DefenderRoll => Roll.DefenderTotal;
        public int Rerolls => Roll.Rerolls;
        public bool AttackerWon => Roll.AttackerWon;

        public Pawn Winner => AttackerWon ? Attacker : Defender;
        public Pawn Loser => AttackerWon ? Defender : Attacker;
        public int ReleasedCount => Rescued?.Count ?? 0;

        public FightResult(
            Pawn attacker, Pawn defender, Vector2Int position, FightRoll roll,
            int woodStolen, int captivesTakenOver, IReadOnlyList<Pawn> rescued, int blocked)
        {
            Attacker = attacker;
            Defender = defender;
            Position = position;
            Roll = roll;
            WoodStolen = woodStolen;
            CaptivesTakenOver = captivesTakenOver;
            Rescued = rescued;
            Blocked = blocked;
        }
    }

    /// Squire vs Squire, Bandit vs Squire and Bandit vs Bandit. A fight is rolled first
    /// (Prepare), can be changed by cards and tie rerolls, and is applied last (Resolve).
    /// The loser dies and goes to its castle.
    public sealed class CombatRules
    {
        private readonly GameState state;
        private readonly RescueRules rescue;
        private readonly CardSystem cards;
        private readonly DiceService dice;

        public CombatRules(GameState state, RescueRules rescue, CardSystem cards, DiceService dice)
        {
            this.state = state;
            this.rescue = rescue;
            this.cards = cards;
            this.dice = dice;
        }

        /// The +N of everything equipped on the pawn. Curses that strip equipment will change
        /// this later, so every caller goes through here.
        public int GetEquipmentModifier(Pawn pawn) => pawn.Equipment.CombatModifier;

        /// All modifiers for one fighter: equipment, the attacker bonus and the abducting penalty.
        public int GetModifier(Pawn pawn, Pawn opponent, bool isAttacker)
        {
            int modifier = GetEquipmentModifier(pawn);

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

        // ---- Who can be fought ----

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

        /// The enemy Squires on a tile, in the order they will be fought.
        public List<Pawn> GetSquireOpponents(Pawn attacker, Vector2Int tile)
        {
            if (!CanFightSquires(attacker))
                return new List<Pawn>();

            return state.FindAll(p =>
                p.Type == PawnType.Squire && !p.AtCastle &&
                p.Position == tile && p.Team != attacker.Team);
        }

        /// The enemy Bandit already hiding where this Bandit has just arrived.
        public bool TryGetOccupant(Pawn arriving, out Pawn occupant)
        {
            occupant = null;

            if (arriving == null || arriving.Type != PawnType.Bandit || arriving.AtCastle)
                return false;

            Pawn other = state.GetBanditAt(arriving.Position, arriving);
            if (other == null || other.Team == arriving.Team)
                return false;

            occupant = other;
            return true;
        }

        // ---- Rolling ----

        /// Rolls both sides' dice. The result is not applied yet.
        public PendingFight Prepare(Pawn attacker, Pawn defender, FightKind kind)
        {
            var fight = new PendingFight(attacker, defender, defender.Position, kind)
            {
                AttackerModifier = GetModifier(attacker, defender, true),
                DefenderModifier = GetModifier(defender, attacker, false)
            };

            fight.AttackerDice = dice.Roll(attacker, RollKind.Combat);
            fight.DefenderDice = dice.Roll(defender, RollKind.Combat);
            return fight;
        }

        /// A tie: both sides roll again.
        public void RerollTie(PendingFight fight)
        {
            fight.AttackerDice = dice.Roll(fight.Attacker, RollKind.Combat);
            fight.DefenderDice = dice.Roll(fight.Defender, RollKind.Combat);
            fight.Rerolls++;
        }

        /// Without a pause: roll ties again until there is a winner (or the cap is reached).
        public void RollOutTies(PendingFight fight)
        {
            while (fight.CanRerollTie)
                RerollTie(fight);
        }

        /// Reroll card: the attacker's die is replaced. A card roll can't create a tie.
        public void RerollAttacker(PendingFight fight)
        {
            fight.AttackerDice = dice.Reroll(
                fight.AttackerDice, r => r.Total + fight.AttackerModifier == fight.DefenderTotal);
        }

        /// Double Roll card: a second die is added to the attacker's. It can't create a tie.
        public void DoubleAttacker(PendingFight fight)
        {
            fight.AttackerDice = dice.DoubleUp(
                fight.AttackerDice, r => r.Total + fight.AttackerModifier == fight.DefenderTotal);
        }

        // ---- Applying ----

        /// Applies the fight: the loser dies, the winner steals. After the tie cap the defender wins.
        public FightResult Resolve(PendingFight fight)
        {
            var roll = new FightRoll(
                fight.AttackerDice, fight.DefenderDice,
                fight.AttackerModifier, fight.DefenderModifier, fight.Rerolls);

            return fight.Kind == FightKind.VsSquire
                ? ResolveSquireFight(fight, roll)
                : ResolveBanditFight(fight, roll);
        }

        private FightResult ResolveSquireFight(PendingFight fight, FightRoll roll)
        {
            Pawn winner = roll.AttackerWon ? fight.Attacker : fight.Defender;
            Pawn loser = roll.AttackerWon ? fight.Defender : fight.Attacker;
            Vector2Int loserSpot = loser.Position;

            TeamState loserTeam = state.Teams[loser.Team];
            int stolen = Mathf.Min(GameConfig.WoodStolenOnSquireFight, loserTeam.Wood);
            loserTeam.Wood -= stolen;
            state.Teams[winner.Team].Wood += stolen;

            // The winner takes 1 piece of the loser's equipment of its choice.
            cards.QueueSteal(winner, loser);

            // A losing Bandit drops its captives. They are freed and run to safety.
            var rescued = new List<Pawn>();
            int blocked = 0;

            if (loser.Type == PawnType.Bandit && loser.IsCarrying)
                blocked = rescue.ReleaseCaptives(loser, loserSpot, new HashSet<Vector2Int>(), rescued);

            state.SendToCastle(loser);

            return new FightResult(
                fight.Attacker, fight.Defender, fight.Position, roll, stolen, 0, rescued, blocked);
        }

        private FightResult ResolveBanditFight(PendingFight fight, FightRoll roll)
        {
            Pawn winner = roll.AttackerWon ? fight.Attacker : fight.Defender;
            Pawn loser = roll.AttackerWon ? fight.Defender : fight.Attacker;

            // The winner takes over the loser's captives if it has room. Anything left
            // is freed and runs to safety, same as after a Bloodhound attack.
            int takenOver = 0;
            foreach (Pawn captive in new List<Pawn>(loser.Captives))
                if (state.TransferCaptive(loser, winner, captive))
                    takenOver++;

            var rescued = new List<Pawn>();
            int blocked = 0;

            if (loser.IsCarrying)
                blocked = rescue.ReleaseCaptives(loser, fight.Position, new HashSet<Vector2Int>(), rescued);

            // The winner steals 1 piece of the loser's equipment of choice.
            cards.QueueSteal(winner, loser);

            state.SendToCastle(loser);

            return new FightResult(
                fight.Attacker, fight.Defender, fight.Position, roll, 0, takenOver, rescued, blocked);
        }
    }
}