using Struikrovers.Core;

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
                            clue + " Equipment and a discard-pile card reward will be added later.");
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
                        $"Player {e.Actor.Team + 1}'s Royalty escaped and must run to safety.");

                case TurnEventKind.SquireKilled:
                    {
                        string clue =
                            $"The Squire of Player {e.Actor.Team + 1} disturbed a hidden Bandit of " +
                            $"Player {e.Target.Team + 1} and was killed!" +
                            (e.Amount > 0 ? $" {e.Amount} wood was stolen." : "");
                        return new TurnMessage(clue, clue + " Equipment stealing will be added later.");
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
                        return new TurnMessage(clue, clue + " Equipment stealing will be added later.");
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

                default:
                    return new TurnMessage(null);
            }
        }

        private static string TypeName(Pawn pawn) =>
    pawn.Type == PawnType.Bandit ? "Bandit" : "Squire";

        private static string DescribeFight(FightResult f)
        {
            string rolls = $"{f.AttackerRoll} vs {f.DefenderRoll}" +
                           (f.Rerolls > 0 ? $", after {f.Rerolls} tie(s)" : "");

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

            return text + " Equipment stealing will be added later.";
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
                default: return "Choose a highlighted tile";
            }
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