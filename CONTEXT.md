# Beep's Number Line Dash

A game that teaches kids to subtract one- and two-digit numbers by hopping backwards along a number line, as fast as
they can.

## Language

### Solving

**Beep**:
The hero: a little desk robot with a screen face, glowing eyes and big headphones.
_Avoid_: player, character, bot

**Problem**:
One subtraction, *start − take-away = answer*, such as 52 − 17. In code: Minuend − Subtrahend.
_Avoid_: question, sum, equation

**Number line**:
The line Beep stands on, from 0 to 10, 20 or 100 depending on the **Stage**.
_Avoid_: track, ruler

**Start flag**:
The green flag the player slides to the first number of the **Problem** and then locks in. It must be on the right
number to lock, and a wrong lock is not a **Slip**.
_Avoid_: left end, handle

**Hop**:
One jump back along the line, of 1 or of 10. The **Hops** chain back from the **Start flag**, and their total is the
number taken away.
_Avoid_: step, jump, move

**Land flag**:
The orange flag at the end of the last **Hop**. When the Hops add up to the take-away number, the Land flag is on the
answer.
_Avoid_: end, right end, result

**GO**:
Sends Beep hopping along the chain, counting each number he lands on. Then the answer is checked.

**Slip**:
A wrong answer: the Hops didn't add up to the take-away number. The right chain is shown, and the Problem comes back
once at the end of the Stage. A second Slip on it shows the answer and moves on.
_Avoid_: mistake (fine in the results ledger), fail, wrong

### Stages

**Stage**:
One stop on the **World Map**: a set of Problems practising one **Skill**, against the clock.
_Avoid_: level, round

**Skill**:
The kind of subtraction a Stage practises, such as hopping past 10 or two-digit minus two-digit.

**Star**:
Up to three per Stage: Clear, No slips, Speedy (beat the clock). The best count is kept.

**Glitch**:
The boss of Glitch Core, the last Stage. Every right answer zaps it.

### Rewards

**Bolt**:
The money earned in Stages and spent in **Gizmo's Garage**.
_Avoid_: coins, points, gold

**XP / Level**:
Earned in Stages. Beep's Level unlocks some items and gives him a title, from Rookie Bot to Legend Bot.

**Gizmo's Garage**:
The shop, run by Gizmo, an old repair robot. It has three shelves: **Parts** (stats with levels), **Gadgets**
(helpers) and **Style** (what Beep wears).
_Avoid_: store, upgrade screen

**Part**:
A shop upgrade with levels that raises one of Beep's stats: SPD (Turbo Treads), LUCK (Bolt Magnet), TIME (Clock Core)
or COMBO (Combo Coil).

**Outfit**:
Beep's headphones, eyes, hat and hop trail, from the Style shelf. One item per slot.

### The World Map

**Gear Island**:
The World Map, drawn in Mode 7: a winding path of **Stops**.

**Stop**:
A place on the path: a Stage or the Garage. Walking past a Stage needs it **Cleared**. The Garage never blocks.

## Example dialogue

> **Dev:** For 13 − 5, can the kid hop back 10 and then forward 5?
> **Designer:** No. Hops only go back. The chain must add up to the take-away number, 5, so the line shows "take away
> 5" literally. Taking back hops (► or RB) is undo, not a forward hop.
> **Dev:** If the kid locks the Start flag on 12 instead of 13?
> **Designer:** It won't lock. Beep says "That's 12. Find 13, more to the right!" It's not a Slip. Finding a number on
> the line is part of the practice.
> **Dev:** On 63 − 27, must they hop 10, 10, then seven 1s?
> **Designer:** Any chain that adds to 27 is right. Twenty-seven 1-hops works, but it's slow, and the clock Star rewards
> using tens. The Hint Lamp shows the neat way: tens first, then ones.
