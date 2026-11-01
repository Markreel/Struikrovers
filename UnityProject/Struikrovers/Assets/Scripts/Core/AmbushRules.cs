using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public enum AmbushKind { None, Abducted, SquireKilled }

    public readonly struct AmbushResult
    {
        public readonly AmbushKind Kind;
        /// The hidden Bandit that sprang the ambush.
        public readonly Pawn Bandit;
        /// Wood the Bandit took (SquireKilled only).
        public readonly int WoodStolen;

        public AmbushResult(AmbushKind kind, Pawn bandit, int woodStolen = 0)
        {
            Kind = kind;
            Bandit = bandit;
            WoodStolen = woodStolen;
        }

        public bool Happened => Kind != AmbushKind.None;
    }

    /// What happens to one type of pawn that disturbs a hidden Bandit.
    public interface IAmbushResponse
    {
        /// Return default (None) when the ambush does not happen after all.
        AmbushResult Respond(Pawn victim, Pawn bandit);
    }

    /// A pawn that chops or loots a hiding place with an enemy Bandit in it gets
    /// ambushed. What that means depends on the pawn type, through the response table.
    /// A pawn type without an entry is not ambushed: the action just works normally.
    public sealed class AmbushRules
    {
        private readonly GameState state;
        private readonly Dictionary<PawnType, IAmbushResponse> responses =
            new Dictionary<PawnType, IAmbushResponse>();

        public AmbushRules(GameState state, CardSystem cards)
        {
            this.state = state;

            responses[PawnType.Royalty] = new AbductionAmbush(state);
            responses[PawnType.Squire] = new SquireKillAmbush(state, cards);
        }

        /// Cards and equipment can change what happens to a pawn type.
        public void SetResponse(PawnType type, IAmbushResponse response)
        {
            if (response == null)
                responses.Remove(type);
            else
                responses[type] = response;
        }

        public AmbushResult Resolve(Pawn victim, Vector2Int tile)
        {
            if (victim == null)
                return default;

            // Your own Bandit hiding there is no danger to you.
            Pawn bandit = state.GetBanditAt(tile);
            if (bandit == null || bandit.Team == victim.Team)
                return default;

            if (!responses.TryGetValue(victim.Type, out IAmbushResponse response))
                return default;

            return response.Respond(victim, bandit);
        }
    }
}