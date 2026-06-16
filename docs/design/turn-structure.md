# Turn Structure — Parallel "Turn-Based"

Ascendant Empire is a turn-based game, but battles run on their own rosters in parallel with the
campaign turn order, so it behaves like a *parallel* turn-based game.

## Rosters

A **roster** determines the order in which players take their turns. There is:

- one roster for the **Campaign Map**, and
- a separate roster for **each Battlefield Map**.

The campaign roster (the sequence in which players take their campaign turn) is determined at the
start of the game. With technology and great expense, a player can move their position in the
queue.

## Campaign turns

On the campaign map each player takes their turn in sequence. When every player has had their turn,
a **round** has occurred.

- A campaign turn **begins** at the end of the previous player's turn.
- It **ends** when either the player signifies they have finished, or the campaign turn timer
  expires — whichever comes first.
- Players may **plan moves in advance** (while it is not their turn). When their turn starts, if no
  adverse changes are detected, the pre-programmed moves are actioned automatically, speeding up the
  turn.

## Battle turns

While engaged in combat on a battlefield, any engaged empires may have successive combat rounds
that occur **outside** the normal play roster.

- Each battlefield runs on its own roster. The **attacking empire normally gets the first turn.**
- A battle turn begins when the previous player on the battle roster completes their turn, or when
  the player's campaign turn begins — whichever comes first.
- Optionally, there may also be a **battle turn timer**.
- A battlefield may run **more than one round** outside of the players' campaign-map turns. But
  every time it becomes a player's turn on the campaign map, they automatically get a turn on each
  battlefield they are engaged in.
- **Reinforcements cannot enter a battlefield map** until it is the player's turn on the campaign
  map.

## Worked example

The table below shows how battlefield rounds (right) interleave with campaign-map turns (left). Here
Player 3 attacks Player 1; the battle resolves over three battlefield rounds spread across two
campaign rounds, ending with Player 3 winning.

| Campaign round | Campaign-map turn | Battlefield turn | Battlefield round | Note |
| --- | --- | --- | --- | --- |
| Round 1 | Player 1 turn | | | |
| | Player 2 turn | | | |
| | Player 3 turn | Player 3 turn | Round 1 | Player 3 attacks Player 1 |
| | Player 4 turn | Player 1 turn | | |
| | Player 5 turn | Player 3 turn | Round 2 | |
| Round 2 | Player 1 turn | Player 1 turn | | |
| | Player 2 turn | Player 3 turn | Round 3 | Player 3 wins |
| | Player 3 turn | | | |
| | Player 4 turn | | | |
| | Player 5 turn | | | |
