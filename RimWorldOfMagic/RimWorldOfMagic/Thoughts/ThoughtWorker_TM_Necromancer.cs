﻿using System;
using Verse;
using RimWorld;

namespace TorannMagic.Thoughts
{
    public class ThoughtWorker_TM_Necromancer : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn pawn, Pawn other)
        {
            if (pawn != null && other != null)
            {
                if (!other.RaceProps.Humanlike || other.Dead)
                {
                    return false;
                }
                if (!RelationsUtility.PawnsKnowEachOther(pawn, other))
                {
                    return false;
                }
                if (pawn.RaceProps.Humanlike && other.RaceProps.Humanlike)
                {
                    if ((pawn.story.traits.HasTrait(TorannMagicDefOf.Paladin) || pawn.story.traits.HasTrait(TorannMagicDefOf.Druid) || pawn.story.traits.HasTrait(TorannMagicDefOf.Priest)) && (other.story.traits.HasTrait(TorannMagicDefOf.Necromancer)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
