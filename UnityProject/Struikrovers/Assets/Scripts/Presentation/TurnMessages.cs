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

                default:
                    return new TurnMessage(null);
            }
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
                    break;

                case BloodhoundAttackState _:
                    message +=
                        " | Bloodhound attack: choose a highlighted bush or tree " +
                        $"within {GameConfig.BloodhoundAttackRange} tiles to attack.";
                    break;

                case ChooseActionState _:
                    message += pawn.Type == PawnType.Squire
                        ? " | Action: click an adjacent tree to chop it, or finish."
                        : " | Action phase: no actions implemented yet. Finish to continue.";
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