using Struikrovers.Core;
using System.Collections.Generic;

namespace Struikrovers.Presentation
{
    public readonly struct TurnMessage
    {
        /// New public clue, or null to leave the current one.
        public readonly string Clue;
        /// Log line for the console, or null for none.
        public readonly string Log;
        public readonly bool IsWarning;

        public TurnMessage(string clue, string log = null, bool isWarning = false)
        {
            Clue = clue;
            Log = log;
            IsWarning = isWarning;
        }
    }

    /// All player-facing wording lives here. Core only raises events.
    public static class TurnMessages
    {
        public static TurnMessage Describe(TurnEvent e, GameState state)
        {
            switch (e.Kind)
            {
                case TurnEventKind.Abducted:
                    return new TurnMessage(
                        "The Bloodhound is ready to pick up the scent.",
                        $"Player {e.Actor.Team + 1} abducted Player {e.Target.Team + 1}'s Royalty!");

                case TurnEventKind.Delivered:
                    {
                        string clue =
                            $"Player {e.Actor.Team + 1} delivered {e.Amount} Royalty " +
                            $"and stole {e.SecondAmount} wood.";
                        return new TurnMessage(
                            clue,
                            clue + " Pick from the discard pile.");
                    }

                case TurnEventKind.Sniffed:
                    return new TurnMessage(GetSniffText(e.Clue));

                case TurnEventKind.Chopped:
                    return new TurnMessage(null,
                        $"Player {e.Actor.Team + 1} chopped a tree and gained {e.Amount} wood. " +
                        $"Total wood: {state.Teams[e.Actor.Team].Wood}");

                case TurnEventKind.Looted:
                    return new TurnMessage(null,
                        $"Team {e.Actor.Team + 1} looted a bush and gained {e.Amount} card(s).");

                case TurnEventKind.AttackInvalidTarget:
                    return new TurnMessage("Choose a highlighted bush or tree to attack.");

                case TurnEventKind.AttackBlocked:
                    return new TurnMessage(null,
                        $"Could not find an empty grass tile for {e.Amount} captive(s); they stay captive.",
                        isWarning: true);

                case TurnEventKind.AttackFoundNothing:
                    {
                        string clue = "The Bloodhound attacks the hiding place, but finds no Bandit.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.AttackNoCaptives:
                    {
                        string clue = "The Bloodhound finds a Bandit, but no captives are released.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.AttackRescued:
                    {
                        string clue = $"The Bloodhound found a Bandit! {e.Amount} captive(s) escaped.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.RescueRunStarted:
                    return new TurnMessage(
                        $"Player {e.Actor.Team + 1}'s Royalty is free! Roll to run out of the hiding place.");

                case TurnEventKind.RescueRunBlocked:
                    {
                        string clue =
                            $"Player {e.Actor.Team + 1}'s Royalty had no way out of the hiding place " +
                            "and was carried to the nearest free tile.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.SquireKilled:
                    {
                        string clue =
                            $"The Squire of Player {e.Actor.Team + 1} disturbed a hidden Bandit of " +
                            $"Player {e.Target.Team + 1} and was killed!" +
                            (e.Amount > 0 ? $" {e.Amount} wood was stolen." : "");
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.StabFoundNothing:
                    {
                        string clue = "The Squire stabs the hiding place, but finds no Bandit.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.StabKilled:
                    {
                        string clue =
                            $"The Squire of Player {e.Actor.Team + 1} stabbed a hiding place and killed " +
                            $"Player {e.Target.Team + 1}'s Bandit!";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.StabKilledOwnBandit:
                    {
                        string clue =
                            $"Oops! The Squire of Player {e.Actor.Team + 1} stabbed its own Bandit, " +
                            "who is sent back to the castle.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.StabFreedCaptives:
                    return new TurnMessage($"{e.Amount} captive(s) were freed and must run to safety.");

                case TurnEventKind.BanditExposed:
                    {
                        string clue =
                            $"The tree is gone! Player {e.Actor.Team + 1}'s Bandit is exposed " +
                            "until it moves away.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.BanditFight:
                case TurnEventKind.SquireFight:
                    {
                        string text = DescribeFight(e.Fight);
                        return new TurnMessage(text, text);
                    }
                case TurnEventKind.CardDrawn:
                    return new TurnMessage(null, $"Player {e.Team + 1} drew a card.");

                case TurnEventKind.CardDiscarded:
                    {
                        string clue = $"Player {e.Team + 1} discarded {e.Card.Name}.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.CardEquipped:
                    {
                        string clue = $"Player {e.Team + 1} equipped {e.Card.Name} on their {e.Actor.BaseType}.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.DragonAwakes:
                    {
                        string clue = "THE DRAGON AWAKES! (The dragon phase isn't built yet, so the game continues.)";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.DeckEmpty:
                    return new TurnMessage("The draw pile is empty.", "The draw pile is empty.");

                case TurnEventKind.EquipmentTaken:
                    {
                        string clue = e.Amount > 0
                            ? $"Player {e.Target.Team + 1}'s {e.Card.Name} was taken instead, by " +
                              $"Player {e.Actor.Team + 1}'s {e.Actor.BaseType}."
                            : $"Player {e.Actor.Team + 1}'s {e.Actor.BaseType} took {e.Card.Name} " +
                              $"from Player {e.Target.Team + 1}'s {e.Target.BaseType}.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.EquipmentMoved:
                    {
                        string line =
                            $"Player {e.Team + 1} moved {e.Card.Name} from their {e.Actor.BaseType} " +
                            $"to their {e.Target.BaseType}.";
                        return new TurnMessage(null, line);
                    }

                case TurnEventKind.AmbushPrevented:
                    // The line reveals that a Bandit was hiding there, so it can be turned off.
                    if (!GameConfig.AnnouncePreventedAmbush)
                        return new TurnMessage(null);

                    return new TurnMessage(null,
                        $"Player {e.Team + 1}'s {e.Card.Name} protected their {e.Actor.BaseType} from an ambush.");

                case TurnEventKind.CardTakenFromDiscard:
                    return new TurnMessage(null,
                        $"Player {e.Team + 1} took {e.Card.Name} from the discard pile.");

                case TurnEventKind.DiceRolled:
                    return new TurnMessage(null,
                        $"Player {e.Team + 1}'s {e.Actor.BaseType} rolled {DescribeDice(e.Dice)} for movement.");

                case TurnEventKind.FightRolled:
                    {
                        string line =
                            $"Fight at tile ({e.Target.Position.x}, {e.Target.Position.y}): " +
                            $"Player {e.Actor.Team + 1}'s {TypeName(e.Actor)} rolled {DescribeDice(e.Dice)}, " +
                            $"Player {e.Target.Team + 1}'s {TypeName(e.Target)} rolled {DescribeDice(e.OtherDice)}. " +
                            $"Totals with modifiers: {e.Amount} vs {e.SecondAmount}" +
                            (e.Amount == e.SecondAmount ? " - a tie!" : ".");
                        return new TurnMessage(null, line);
                    }

                case TurnEventKind.CardPlayed:
                    return new TurnMessage(null, $"Player {e.Team + 1} played {e.Card.Name}.");

                case TurnEventKind.EquipmentNabbed:
                    {
                        string clue = e.Amount > 0
                            ? $"Player {e.Team + 1} nabbed Player {e.Target.Team + 1}'s {e.Card.Name}, " +
                              "which was taken instead of the chosen item."
                            : $"Player {e.Team + 1} nabbed {e.Card.Name} from " +
                              $"Player {e.Target.Team + 1}'s {e.Target.BaseType}.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.CardYoinked:
                    {
                        string clue = $"Player {e.Team + 1} yoinked a card from Player {e.Amount + 1}'s hand.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.CardPlayCancelled:
                    return new TurnMessage(null, $"Player {e.Team + 1} took {e.Card.Name} back into their hand.");

                case TurnEventKind.HandsSwapped:
                    {
                        string clue = $"Player {e.Team + 1} swapped hands with Player {e.Amount + 1}.";
                        return new TurnMessage(clue, clue);
                    }

                case TurnEventKind.EquipmentSwapped:
                    {
                        string clue =
                            $"Player {e.Actor.Team + 1}'s {e.Actor.BaseType} and " +
                            $"Player {e.Target.Team + 1}'s {e.Target.BaseType} swapped all their equipment.";
                        return new TurnMessage(clue, clue);
                    }

                default:
                    return new TurnMessage(null);
            }
        }

        private static string TypeName(Pawn pawn) =>
    pawn.Type == PawnType.Bandit ? "Bandit" : "Squire";

        private static string RollText(int die, int modifier) =>
    modifier == 0 ? die.ToString() : $"{die + modifier} ({die}{modifier:+0;-0})";

        private static string DescribeFight(FightResult f)
        {
            FightRoll r = f.Roll;
            string rolls =
                $"{RollText(r.AttackerDie, r.AttackerModifier)} vs {RollText(r.DefenderDie, r.DefenderModifier)}" +
                (r.Rerolls > 0 ? $", after {r.Rerolls} tie(s)" : "");

            string text =
                $"A fight broke out at tile ({f.Position.x}, {f.Position.y})! " +
                $"Player {f.Winner.Team + 1}'s {TypeName(f.Winner)} beat " +
                $"Player {f.Loser.Team + 1}'s {TypeName(f.Loser)} ({rolls}), " +
                "and the loser was sent back to its castle.";

            if (f.WoodStolen > 0)
                text += $" {f.WoodStolen} wood was taken.";

            if (f.CaptivesTakenOver > 0)
                text += $" The winner took over {f.CaptivesTakenOver} captive(s).";

            if (f.ReleasedCount > 0)
                text += $" {f.ReleasedCount} captive(s) escaped.";

            return text;
        }

        public static string GetSniffText(SniffClue clue)
        {
            switch (clue)
            {
                case SniffClue.NoCaptive: return "No captive to track.";
                case SniffClue.Nearby: return "The Bloodhound senses the captive nearby!";
                case SniffClue.Stronger: return "The scent is getting stronger!";
                case SniffClue.Fainter: return "The scent is growing fainter...";
                case SniffClue.Unchanged: return "The scent hasn't changed.";
                default: return "The Bloodhound catches a scent, but its direction is unclear.";
            }
        }

        public static string GetPrimaryLabel(PrimaryAction action)
        {
            switch (action)
            {
                case PrimaryAction.Finish: return "Finish Activation";
                case PrimaryAction.MoveBandit: return "Move Bandit";
                case PrimaryAction.Roll: return "Roll Movement";
                case PrimaryAction.KeepResult: return "Keep result";
                case PrimaryAction.RerollTie: return "Reroll tie";
                default: return "Choose a highlighted tile";
            }
        }

        /// Text of one entry in the menu for an equipped item.
        public static string GetManageLabel(ManageOption option, Pawn pawn, EquipmentSlot slot)
        {
            switch (option.Kind)
            {
                case ManageKind.MoveEquipment:
                    return $"Move to the {option.Target.BaseType}";

                case ManageKind.SwapEquipment:
                    return $"Swap with the {option.Target.BaseType}'s {option.Target.Equipment.Get(slot).Name}";

                default:
                    return $"Discard {pawn.Equipment.Get(slot).Name}";
            }
        }

        /// "Keep result" only makes sense while you could still play a roll card. Otherwise it is just "Continue".
        public static string GetPrimaryLabel(TurnManager turn, ITurnState step)
        {
            if (step.Primary == PrimaryAction.KeepResult && !turn.RollCardAvailable)
                return "Continue";

            return GetPrimaryLabel(step.Primary);
        }

        public static string GetFighterCaption(Pawn pawn, bool attacker) =>
            $"{TypeName(pawn)} {(attacker ? "attacks" : "defends")}";

        private static string DescribeDice(DiceRoll roll)
        {
            var parts = new List<string>();

            foreach (DieResult die in roll.Dice)
            {
                if (!die.HasOther)
                    parts.Add(die.Kept.ToString());
                else if (roll.Mode == RollMode.Advantage)
                    parts.Add($"{die.Kept} (best of {die.Kept} and {die.Other})");
                else
                    parts.Add($"{die.Kept} (worst of {die.Kept} and {die.Other})");
            }

            string text = string.Join(" + ", parts);
            if (roll.Dice.Count > 1) text += " = " + roll.Total;
            if (roll.Doubled) text += ", double roll";
            if (roll.Rerolled) text += ", rerolled";
            return text;
        }

        public static string BuildStatus(TurnManager turn, string clue)
        {
            Pawn pawn = turn.ActivePawn;
            ITurnState step = turn.CurrentState;
            if (pawn == null || step == null)
                return "";

            int team = turn.CurrentTeam;
            TeamState stats = turn.State.Teams[team];

            string message =
                "<size=120%>TEAM " + (team + 1) + "  •  " +
                pawn.Type.ToString().ToUpperInvariant() + "'S TURN</size>\n";

            if (pawn.AtCastle)
                message += " (at castle)";

            message += $" | Wood: {stats.Wood} | Cards: {stats.Cards}";

            switch (step)
            {
                case BloodhoundMoveState _:
                    message +=
                        $" | Bloodhound movement: {turn.BloodhoundMovesRemaining} move(s) remaining. " +
                        "Choose a highlighted empty tile.";
                    break;

                case AbductionPreviewState preview:
                    message +=
                        $" | Abduction preview: {preview.Plan.StepsToRoyalty} steps to Royalty, " +
                        $"{preview.Plan.RemainingMovement} movement steps left. " +
                        "Choose a highlighted destination or cancel.";
                    break;

                case ChooseDestinationState _:
                    message +=
                        $" | Move up to {turn.RolledMovement} tiles. " +
                        "Orange Royalties can be abducted; yellow tiles are destinations.";

                    if (step.MoveFightTargets.Count > 0)
                        message += " Pink Squires block your path: click one to fight it and keep moving (this doesn't use your action).";

                    if (step.BlockedDestinations.Count > 0)
                        message += " Dark red tiles are behind enemy Squires: click one to fight your way there.";

                    if (turn.MovementRollOpen && turn.RollCardAvailable)
                        message += " You can still play Reroll or Double Roll (click the card in your hand).";
                    break;

                case BloodhoundAttackState _:
                    message +=
                        " | Bloodhound attack: choose a highlighted bush, tree or exposed Bandit " +
                        $"within {GameConfig.BloodhoundAttackRange} tiles to attack.";
                    break;

                case ChooseActionState _:
                    message += " | Action: click a highlighted tile (pink = fight or abduct), or finish.";

                    if (step.AvailableActionGroups.Count > 1)
                    {
                        foreach (ActionGroupInfo group in step.AvailableActionGroups)
                        {
                            if (group.Id == step.SelectedActionGroup)
                                message += " Mode: " + group.Label + ".";
                        }
                    }
                    break;

                case PromptState _:
                    message += step.CanCancel
                        ? " | Make a choice in the panel, or click Cancel (Esc) to take the card back."
                        : " | Make a choice in the panel.";
                    break;

                case FightPauseState pause:
                    {
                        PendingFight f = pause.Fight;
                        bool canPlay = turn.RollCardAvailable;

                        message += $" | Fight! {f.AttackerTotal} vs {f.DefenderTotal}. ";

                        if (f.CanRerollTie) message += "It's a tie: click Reroll tie.";
                        else message += canPlay ? "Click Keep result to continue." : "Click Continue.";

                        if (canPlay)
                            message += " You can play Reroll or Double Roll by clicking the card in your hand.";

                        break;
                    }

                default: // AwaitRollState
                    message += pawn.Type == PawnType.Bandit
                        ? $" | Move up to {GameConfig.BanditMoveSteps} tiles."
                        : " | Roll to determine movement.";
                    break;
            }

            if (!string.IsNullOrEmpty(clue))
                message += "\n" + clue;

            return message;
        }
    }
}