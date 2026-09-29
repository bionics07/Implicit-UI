# Asset Store listing

The texts submitted with the package, kept here so the next update starts from what is already published.
Package title: **Implicit UI — uGUI Essentials: Fill, Grayscale, Hitbox & Text Tools**. Category: Tools/GUI.

## Summary (10-200 characters)

Five small fixes for Unity UI, each one component away: sliced fills, automatic grayscale, bigger touch
areas, matching text sizes and bulk font swaps.

## Description

Implicit UI is a small set of uGUI improvements built on one rule: adding the component should be enough.
No material to drag in, no setup prefab, no manager object in the scene, no central asset to configure.

Each feature fixes something you have probably worked around by hand: a health bar whose frame stretches
because Unity's Filled image drops Sliced, a button that has to be greyed out from script every time it is
disabled, an icon too small to tap on a phone, three menu labels that auto-size to three different sizes, and
a font swap that means opening forty prefabs one by one.

Everything works with the components you already have. Implicit Fill reads the Image's own Fill Amount, Fill
Method and Fill Origin, so code that animates image.fillAmount keeps working. Implicit Grayscale reads the
Button's interactable state. Implicit Text Size Group only lowers each text's auto size maximum, so auto size
stays on and removing the group puts every value back.

The package has no dependencies beyond com.unity.ugui, does not use reflection, and ships with full C# source.
TextMeshPro is optional: the text features use it when it is installed and work without it. It runs on
Built-in, URP and HDRP, in Screen Space Overlay, Screen Space Camera and World Space, verified by hand in all
three pipelines and on an Android device.

A sample scene for each feature is included, and the package is also open source under MIT on GitHub, where
issues and questions are welcome. If it saves you an afternoon, you can buy me a coffee at
https://ko-fi.com/bionics07 - entirely optional, and nothing in the package asks for it.

## Technical details

- **Implicit Fill** — fills an Image that stays Sliced, Tiled or Simple, so a framed bar keeps its borders
  while it fills. Every fill method: Horizontal, Vertical, and Radial 90, 180 and 360, clockwise or not,
  matching what a native Filled image draws. Changing the fill never rebuilds layout.
- **Implicit Grayscale** — turns an Image or RawImage gray on its own while its Button is not interactable,
  with an optional tint over the gray for sepia or cold looks. The gray amount travels in the vertex data, so
  images with different amounts still batch together. Its inspector shows which button or parent controls it
  and which images below follow it.
- **Implicit Hitbox** — a minimum touch size in dp (48 by default, the size Android recommends), applied while
  the game runs on top of Unity's own Raycast Padding, plus an alpha hit test that works with padding and with
  sprite atlases. It warns once instead of on every touch when a texture cannot be read, and enables Read/Write
  with one click after telling you what it costs in memory.
- **Implicit Text Size Group** — every auto-sized text below it takes the smallest size any of them would pick
  alone, so "OK", "Settings" and "Back to the main menu" stop being three different sizes. Legacy Text and
  TextMeshPro, mixed. Sizes are measured in the text's own units, so they do not change with the Game view or
  the device resolution. In the Editor it is a preview: the authored values go back before anything is saved.
- **Font Changer** (Tools > Implicit UI > Font Changer) — changes the font of every text in the open scenes or
  in a folder of prefabs, after a dry run that lists each text with a checkbox. Values a prefab instance
  inherits stay on the prefab instead of becoming an override on every instance, TextMeshPro materials are
  checked against the target font, and material presets are kept unless you choose to replace them.

**Compatibility**

- Unity 2021.3 and newer. Tested on 2021.3, 2022.3, 6000.0 and 6000.6 on every commit.
- Built-in, URP and HDRP, in Screen Space Overlay, Screen Space Camera and World Space.
- Dependencies: com.unity.ugui only. TextMeshPro optional.
- Full C# source, XML documentation on the public API, no reflection.
- 153 automated tests. Verified by hand in the Editor and on an Android device (IL2CPP, ARM64).
- One sample scene per feature.
- MIT licensed and open source: https://github.com/bionics07/Implicit-UI

## AI/ML disclosure

I used an AI coding assistant (Anthropic's Claude, through Claude Code) as a pair programmer while building
this package. It helped draft and refactor C# code, write the automated tests and write the documentation,
following my own design decisions and specifications.

Every file was reviewed and accepted by me before being committed. Every feature was also verified by hand in
the Unity Editor and on an Android device, on top of the 153 automated tests that run on Unity 2021.3, 2022.3,
6000.0 and 6000.6.

No AI-generated art, audio, models or textures are included. The sample scenes use Unity's own built-in UI
sprites and fonts, and the screenshots are captures of those scenes. No third-party content was generated or
reproduced by AI.

## Link to documentation

https://github.com/bionics07/Implicit-UI
