# Character animation lab

`Assets/Scenes/Sandbox/CharacterAnimationLab.unity` is a scene of its own for
working out the hero's run on a procedural 2D skeleton before any final art.
It is not in the build's scene list and nothing of the game is in it or
changed by it. `Descender > Lab > Character animation lab` makes it again.

## What is in it

- **The runner** (`Descender.Motion.RunSkeleton`): a skeleton of 19 bones
  built in code: hips, spine, chest, neck, head; each side's shoulder,
  upper arm, lower arm and hand; each side's thigh, shin and foot. Plain
  shapes are drawn on them. It is seen from its left, so the left limbs are
  nearer and the right ones behind and darker.
- **The run** (`Descender.Motion.RunGait`): eight key poses a cycle,
  contact, down, passing and flight for the right leg and then the left
  (`RunKey`). Each foot is on the ground 34% of the cycle, so there are two
  flights a cycle and the feet are never both down.
  - A foot on the ground lies still on it while the hips travel over it at
    the running speed. In the last part of its stance the heel lifts and
    the foot turns about its toes, which stay where they came down.
  - A foot in the air follows a curve through the key poses: up behind, the
    heel toward the seat, then tucked under the hips, then the knee driving
    forward, then reaching out and coming back a little to meet the ground.
  - The legs are solved by two-bone IK from hip to ankle with the knee
    forward, so thigh and shin keep their lengths.
  - The hips are set as high as the feet on the ground can be reached from.
    They rise and fall over each step, lowest at "down".
  - The whole body leans forward from the feet (they fall behind the hips),
    the torso leans further from the hips, and the hips and shoulders turn against each
    other. The arms swing against the legs with bent elbows.
  - Between key poses the channels ease along a Catmull-Rom curve.
  - Phase and travel come from the same time each frame, so the speed of
    play does not change the motion.
- **The overlay** (`RunDebugOverlay`) shows:
  - the bones as lines and the joints as dots;
  - a green ring under each foot while it is on the ground;
  - a green mark where each foot came down: a foot that slid would leave its
    ring behind its mark;
  - in the top left: the phase, the nearest key pose, which feet are down,
    and the speed, stride, cycle and lean.

## The knight

The runner wears the crowned knight (`RunKnightSkin`), from the owner's parts
sheet `tools/hero_knight/source/parts_sheet.webp`. That sheet shows the knight
drawn apart, every part whole, at one scale, in side view.

`tools/hero_knight/parts.py` cuts the sheet into 11 parts in
`Assets/Art/Sprites/hero/parts/`. Each part's pivot is at its joint, and
`parts.json` records how each part is worn. The parts are:

- the cloak, worn on the neck;
- the torso with its tassets, on the hips;
- the tabard and the head with its hair;
- the pauldron, on the shoulder;
- the sleeve, forearm, thigh, shin and foot;
- the greatsword, in the far hand.

The upper arm is cut in two. The pauldron turns with the shoulders, and the
sleeve swings under it. The tool carries the sleeve up under the pauldron in a
disc about the shoulder joint, so the cut never shows as the arm swings.

How the parts sit on the bones:

- Limbs are stretched along their bones. They are worn on both sides: the far
  side darker and behind, the thighs under the torso's tassets.
- The torso, head, cloak and tabard turn with their bones, and the cloak and
  tabard sway each step.
- The sword is carried low behind at a steady angle.
- The runner has the knight's lengths, set by `RunBody.Knight`.

With the knight worn, the bones overlay starts hidden. K takes the knight off
and puts him back on.

## Settings (Runner, in the inspector, also while playing)

| Field | What it does |
| --- | --- |
| Derived | which of speed, stride and cycle follows from the other two (speed = stride / cycle) |
| Running speed | units a second the body travels |
| Stride length | units covered in a whole cycle, a step of each leg |
| Cycle duration | seconds for a whole cycle |
| Body lean | degrees the whole body leans forward from the feet |
| Torso lean | degrees the torso leans forward from the hips |
| Vertical bounce | units the hips rise and fall |
| Arm swing | degrees each upper arm swings |
| Smoothing | 0: straight from key to key; 1: eased through them |
| Counter rotation | how far hips and shoulders turn against each other |
| Body | the bones' lengths |

## How to test it

1. Open `Assets/Scenes/Sandbox/CharacterAnimationLab.unity`, or make it
   with `Descender > Lab > Character animation lab`, and press Play.
2. Keys:
   - Tab shows or hides the overlay (the bones, contacts and numbers).
   - K takes the knight off and puts him back on.
   - Space pauses.
   - The right arrow steps one sixtieth of a second while paused.
   - 1, 2 and 3 play at full, a quarter and a tenth speed.
3. Watch the green rings: each must sit on its mark until the foot lifts.
4. Select `CharacterAnimationLab/Runner` and change the numbers while it
   runs.
5. `Descender > Lab > Picture the run's key poses` writes the eight key
   poses and sixteen moments of the cycle to
   `Logs/CharacterAnimationLab/run.png`.
6. `Descender > Lab > Build character animation lab` makes a Windows player
   of the scene alone at `Build/CharacterAnimationLab64/`.

The tests are `RunGaitTests` (EditMode) and `CharacterAnimationLabTests`
(PlayMode). They check:

- the eight key poses are distinct;
- the bones keep their lengths;
- a foot does not slide;
- the legs alternate, with two flights a cycle;
- the knees bend forward;
- the bounce is small;
- the hips and shoulders turn against each other and the arms swing
  against the legs;
- a foot has no jump where it lands or leaves the ground;
- speed, stride and cycle stay tied together;
- the scene plays.
