# Train Guess CCTV Prototype

Greybox prototype for the fixed-camera train guessing scene.

## Open

- Scene: `Assets/TrainGuessPrototype/Scenes/TrainGuessCCTVPrototype.unity`

Maintain the scene directly in Unity. Adjust timing and story beats in
`Scripts/TrainGuessDirector.cs`.

## Implemented Flow

1. A legacy AnimationClip pulls the CCTV camera from the hand to an overhead view.
2. The capsule player descends from the second-floor waiting hall.
3. Leaving the first camera swaps to the fixed platform camera with a hard feed cut.
4. Reaching the far platform end opens the repeating jump confirmation prompt.
5. Confirming is interrupted by the newspaper reader's invitation.
6. Two free time/destination guesses summon matching trains that stop briefly before departing.
7. The final selector is locked to `12:00 / MUSEUM`.
8. The stopped train occludes the player while the player is removed.
9. A fast train occludes the reader while the reader is replaced by the newspaper and hat.

## Prototype Controls

- Move: WASD, arrow keys, or left stick.
- Selector focus: mouse pointer, A/D, left/right, or D-pad.
- Change selected value: mouse wheel, W/S, up/down, or D-pad.
- Confirm: UI button, Enter, or gamepad south button.

Hours wrap through `00-23` and minutes wrap through `00-59`, so every submitted
time is a legal 24-hour time. Selecting an earlier time advances to its next daily
occurrence during the CCTV time lapse.
