/*
    Weather ending
    Sand damage / Hail damage / Ice Body / Dry Skin (rain or sun) / Solar Power / Rain Dish
    Pending Emergency Exit / Wimp Out switches resolve
    High affection curing major status condition
    Future Sight / Doom Desire / Wish (activate in the order they were set, like a queue; not determined by Speed)
    First event block
        Side conditions: G-Max chip (Cannonade, Vine Lash, Volcalith, Wildfire) and Pledge Sea of Fire. (activate in the order they were set, like a queue; not determined by Speed)
        Grassy Terrain
        Healer / Hydration / Shed Skin
        Black Sludge (healing / damaging) / Leftovers
    Pending Emergency Exit / Wimp Out switches resolve
    Aqua Ring
    Ingrain
    Leech Seed (the Pokemon affected by Leech Seed, i.e. the Pokemon losing HP)
    Regular Poison / Toxic / Poison Heal
    Burn
    Curse
    Binding moves (both taking damage / being freed from)
    Octolock
    Taunt ending
    Torment ending
    Encore ending
    Disable ending
    Magnet Rise ending
    Yawn
    Perish count
    Roost user regaining Flying-type
    Pending Emergency Exit / Wimp Out switches resolve
    Second event block
        Reflect ending
        Light Screen ending
        Safeguard ending
        Mist ending
        Tailwind ending
        Pledge Rainbow ending
        Pledge Sea of Fire ending
        Pledge Swamp ending
        Aurora Veil ending
    Trick Room ending
    Gravity ending
    Wonder Room ending
    Magic Room ending
    Terrain ending
    Third event block
        Uproar (active / ending)
        Bad Dreams / Ball Fetch / Harvest / Moody / Pickup / Slow Start / Speed Boost
        Flame Orb / Sticky Barb / Toxic Orb / White Herb
    Pending Emergency Exit / Wimp Out switches resolve
    Power Construct / Schooling / Shields Down / Zen Mode (uses raw Speed with a non-RNG based Speed tie to determine which does a form change first)
    Fourth event block
        Hunger Switch
        Eject Pack


Various notes and observations:

    Torment ending is from G-Max Meltdown, G-Max Melmetal's move. Although it doesn't say so on the Y-info screen, its Torment effect wears off after 3 turns.
    Steps 5 and 6-1 are queue-based. That is, whichever effect was set first will activate first. So if Sea of Fire is set first, then G-Max chip damage (on the same side), it will damage from Sea of Fire -> G-Max chip and vice versa.
        I am near certain Future attacks / Wish should be lumped in with the rest of Step 6, but I can't explain the behavior of things like faster Doom Desire -> slower Doom Desire -> faster Grassy Terrain -> faster Leftovers -> slower Grassy Terrain -> slower Leftovers.
    Step 25 is host-based. Whoever has their trainer card on the left side when the match starts is host, and they will see side effects on their side wear off prior to the opponent's. Court Change has no impact on this; the host's side will have their effects wear off first, not the host's effects (so it's unlike how, say, Court Change + Sticky Web + Defiant will fail to give your Pokemon an attack boost).
    Step 33 is raw Speed with a non-RNG based Speed tie. See this post for an overview of what that means.
    Multiple Emergency Exit / Wimp Out can activate throughout the end-of-turn effects. For example, a Poisoned EE and a burned EE will both switch out after being put under half HP, and then both Pokemon are replaced at step 24.
        This is not how Eject Pack behaves; if multiple Pokemon could activate Eject Pack, the fastest Pokemon at the time of Eject Pack activating is the one that sees theirs trigger.
        Eject Pack will activate immediately in response to switching in after say, Emergency Exit and getting a Sticky Web drop.
    Surprisingly, end-turn resolution order does not use the typical dynamic Speed interactions of Gen 8. The timing of when and how Speed updates during end-turn resolution is identical to Gen 7.
    Although Roost doesn't have a message at the end of the turn indicating where it wears off, a series of tests from SnorlaxMonster were used to determine its placement after Perish Song, but before the next Emergency Exit switchin:
        A Flying-type Pokemon with Roost holds a super Sitrus Berry that will confuse the user and uses Roost on the same turn that an opposing Unnerve Pokemon would faint from Perish Song, in Misty Terrain. After using Roost, the user is knocked down into lower HP to allow the super Sitrus Berry to meet the HP condition to activate. The Roost user in this case is not confused, confirming it is still grounded and that Roost has not worn off yet.
        A Flying-type Pokemon with Roost holds an Adrenaline Orb, paired with an ally Symbiosis Pokemon holding a super Sitrus Berry that will confuse the Roost user, in Misty Terrain. After using Roost, the user is knocked down into lower HP to allow the super Sitrus Berry to meet the HP condition to activate. An opposing Emergency Exit kicks in from poison, replacing the Emergency Exit Pokemon with an Intimidate Pokemon, which triggers Adrenaline Orb, which triggers Symbiosis, which causes the Pokemon to eat the super Sitrus Berry. The Roost user in this case is confused, confirming it is no longer grounded and that Roost has worn off.
    The following effects are impossible to test in Gen 8 that were in Gen 7: Nightmare, Telekinesis, Heal Block, Embargo, Lucky Chant, Water Sport, and Mud Sport.
*/
