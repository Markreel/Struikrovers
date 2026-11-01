using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public enum AmbushKind { None, Abducted, SquireKilled, Prevented }

    public readonly struct AmbushResult
    {
        public readonly AmbushKind Kind;
        /// The hidden Bandit that sprang (or would have sprung) the ambush.
        public readonly Pawn Bandit;
        /// Wood the Bandit took (SquireKilled only).
        public readonly int WoodStolen;
        /// The item that stopped the ambush (Prevented only).
        public readonly CardDefinition PreventedBy;

        public AmbushResult(AmbushKind kind, Pawn bandit, int woodStolen = 0, CardDefinition preventedBy = null)
        {
            Kind = kind;
            Bandit = bandit;
            WoodStolen = woodStolen;
            PreventedBy = preventedBy;
        }

        /// True when the victim was really ambushed (a prevented ambush did not happen).
        public bool Happened => Kind != AmbushKind.None && Kind != AmbushKind.Prevented;
    }

    /// What happens to one type of pawn that disturbs a hidden Bandit.
    public interface IAmbushResponse
    {
        /// True when the ambush would work right now (for example the Bandit has room for a captive).
        bool CanRespond(Pawn victim, Pawn bandit);

        /// Return default (None) when the ambush does not happen after all.
        AmbushResult Respond(Pawn victim, Pawn bandit);
    }

    /// A pawn that chops or loots a hiding place with an enemy Bandit in it gets
    /// ambushed. What that means depends on the pawn type, through the response table.
    /// A pawn type without an entry is not ambushed: the action just works normally.
    /// Equipment can stop an ambush (Charming Chestplate).
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

            // Nothing to prevent when the ambush couldn't happen anyway.
            if (!response.CanRespond(victim, bandit))
                return default;

            if (EquipmentEffects.PreventsAmbush(victim, bandit, out CardDefinition source))
                return new AmbushResult(AmbushKind.Prevented, bandit, 0, source);

            return response.Respond(victim, bandit);
        }
    }
}