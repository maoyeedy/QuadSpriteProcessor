# Quad-Divisible Sprite Processor

<img src="Documentation~/editor-warning.png" width="600" alt="Editor Warning"/>

## Why Quad Divisible?

During school projects, the artists have been always giving me sprites with odd dimensions like 543x981. These can't be compressed to DXT/BC formats, thus bloating our final build size.

<div align="center">
  <table>
    <tr>
      <td align="center"><img src="Documentation~/size-original.png" width="400" alt="Original texture"/><br><b>Original</b></td>
      <td align="center"><img src="Documentation~/size-quad-divisible.png" width="400" alt="Quad-divisible texture"/><br><b>Quad-Divisible (Way Smaller)</b></td>
    </tr>
  </table>
</div>

## Feature

- Resize textures to be quad-divisible with minimal quality loss
- Batch process multiple textures at once
- Works with PNG, JPG, TGA, EXR (TGA and EXR are usually quad-divisible already)

## How to Install

Package Manager - Install Package from Git URL
```
https://github.com/Maoyeedy/QuadSpriteProcessor.git
```

## How to Use

### 1. Context Menu
- (Multi-Select and) right-click assets in project panel.
- "Resize to be Quad-Divisible"

### 2. Editor window
- Tools → Texture Processing
- Set path, scan, select, process

 <img src="Documentation~/editor-window.png" width="400" alt="Editor Warning"/>

## TODO
- Bug Fixes (There can definitely be potential bugs, so Issues/PR are welcome)
- An option to use it as AssetPostprocessor, with some matching rules. So that it will auto-convert every sprite you import.
- Backup of the original sprites? (I think this bloats your unity project size. Also if you want to revert them, why not rely on VCS)

## More Background Stories

As most of my projects are showcased with WebGL, I've been trying to squeeze my build size even smaller for faster loading time.

(I highly recommend [ProjectAuditor](https://github.com/Unity-Technologies/ProjectAuditor), or manually open your Editor.log to check 'Build Report'.)

When I found that DXT/BC only work on quad-divisible textures, I wrote a [custom powershell script](https://gist.github.com/Maoyeedy/769ad8f2f4faf3f5c219b07658bc3880), using ImageMagick to recursively process all textures in the project. But that can be destructive and not that user-friendly. So I decided to integrate it into Unity Editor.
