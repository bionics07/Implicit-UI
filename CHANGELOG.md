# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Implicit Fill: fills an Image that stays Sliced, Tiled or Simple, so a framed bar can be resized without
  stretching its borders. It uses the Image's own Fill Amount, Fill Method and Fill Origin, so code that animates
  `image.fillAmount` keeps working, and changing the fill never rebuilds layout. Every fill method: Horizontal,
  Vertical, and Radial 90, 180 and 360 with Clockwise, matching what a native Filled image draws.
- Implicit Grayscale: turns an Image or RawImage gray, on its own when its button is disabled, with an optional
  tint over the gray. No material to set up, and images with different gray amounts still batch together.
  Its inspector shows which button or parent image makes it gray, and which images below it follow it.
- Implicit Hitbox: keeps small graphics easy to tap with a minimum touch size in dp (48 by default), applied
  while the game runs on top of the native Raycast Padding, and an alpha hit test that works with padding and
  sprite atlases, warns once when the texture cannot be read, and enables Read/Write with one click.
