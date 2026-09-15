# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Implicit Fill: fills an Image that stays Sliced, Tiled or Simple, so a framed bar can be resized without
  stretching its borders. It uses the Image's own Fill Amount, Fill Method and Fill Origin, so code that animates
  `image.fillAmount` keeps working, and changing the fill never rebuilds layout. Horizontal and Vertical fill.
